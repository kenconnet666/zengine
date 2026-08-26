using ZEngine.Graphics;
using ZEngine.RenderGraph;
using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public sealed unsafe class VulkanClearRenderer : IDisposable
{
    private const int FramesInFlight = 2;
    private const uint QueueFamilyIgnored = uint.MaxValue;
    private readonly VulkanDevice _device;
    private readonly VulkanSwapchain _swapchain;
    private readonly VulkanTrianglePipeline? _trianglePipeline;
    private readonly VulkanTimeline _timeline;
    private readonly FrameRetirementQueue _retirementQueue = new();
    private readonly CompiledRenderGraph _renderGraph;
    private readonly FrameGraphSink _graphSink;
    private readonly VkImageView[] _imageViews;
    private readonly bool[] _initializedImages;
    private readonly delegate* unmanaged<VkDevice, VkImageView, void*, void>
        _destroyImageView;
    private readonly delegate* unmanaged<VkDevice, VkCommandPool, void*, void>
        _destroyCommandPool;
    private readonly delegate* unmanaged<VkDevice, VkSemaphore, void*, void>
        _destroySemaphore;
    private readonly delegate* unmanaged<VkDevice, VkSwapchainKHR, ulong, VkSemaphore, VkFence, uint*, VkResult>
        _acquireNextImage;
    private readonly delegate* unmanaged<VkCommandBuffer, uint, VkResult>
        _resetCommandBuffer;
    private readonly delegate* unmanaged<VkCommandBuffer, VkCommandBufferBeginInfo*, VkResult>
        _beginCommandBuffer;
    private readonly delegate* unmanaged<VkCommandBuffer, VkResult>
        _endCommandBuffer;
    private readonly delegate* unmanaged<VkCommandBuffer, VkDependencyInfo*, void>
        _cmdPipelineBarrier2;
    private readonly delegate* unmanaged<VkCommandBuffer, VkRenderingInfo*, void>
        _cmdBeginRendering;
    private readonly delegate* unmanaged<VkCommandBuffer, void>
        _cmdEndRendering;
    private readonly delegate* unmanaged<VkQueue, uint, VkSubmitInfo2*, VkFence, VkResult>
        _queueSubmit2;
    private readonly delegate* unmanaged<VkQueue, VkPresentInfoKhr*, VkResult>
        _queuePresent;
    private readonly FrameResources[] _frames;
    private readonly ulong[] _imageTimelineValues;
    private readonly VkSemaphore[] _renderFinished;
    private int _nextFrame;
    private bool _disposed;

    public ulong LastSubmittedTimelineValue { get; private set; }

    public ulong CompletedTimelineValue => _timeline.CompletedValue;

    public CompiledRenderGraph FrameGraph => _renderGraph;

    public int FrameResourceCount => _frames.Length;

    public VulkanClearRenderer(
        VulkanDevice device,
        VulkanSwapchain swapchain,
        VulkanTrianglePipeline? trianglePipeline = null)
    {
        _device = device;
        _swapchain = swapchain;
        _trianglePipeline = trianglePipeline;
        _timeline = device.CreateTimeline();

        var createImageView =
            (delegate* unmanaged<VkDevice, VkImageViewCreateInfo*, void*, VkImageView*, VkResult>)device.GetProcAddress(
                "vkCreateImageView\0"u8);
        _destroyImageView =
            (delegate* unmanaged<VkDevice, VkImageView, void*, void>)device.GetProcAddress(
                "vkDestroyImageView\0"u8);

        _imageViews = new VkImageView[swapchain.Images.Count];
        _initializedImages = new bool[swapchain.Images.Count];
        _renderFinished = new VkSemaphore[swapchain.Images.Count];
        for (int index = 0; index < swapchain.Images.Count; index++)
        {
            VkImageViewCreateInfo createInfo = new()
            {
                SType = VkStructureType.ImageViewCreateInfo,
                Image = swapchain.Images[index],
                ViewType = VkImageViewType.Image2D,
                Format = swapchain.SurfaceFormat.Format,
                Components = new()
                {
                    R = VkComponentSwizzle.Identity,
                    G = VkComponentSwizzle.Identity,
                    B = VkComponentSwizzle.Identity,
                    A = VkComponentSwizzle.Identity
                },
                SubresourceRange = ColorSubresourceRange()
            };

            VkImageView imageView = default;
            VulkanException.ThrowIfFailed(
                createImageView(
                    device.Handle,
                    &createInfo,
                    null,
                    &imageView),
                "vkCreateImageView");
            _imageViews[index] = imageView;
        }

        var createCommandPool =
            (delegate* unmanaged<VkDevice, VkCommandPoolCreateInfo*, void*, VkCommandPool*, VkResult>)device.GetProcAddress(
                "vkCreateCommandPool\0"u8);
        _destroyCommandPool =
            (delegate* unmanaged<VkDevice, VkCommandPool, void*, void>)device.GetProcAddress(
                "vkDestroyCommandPool\0"u8);
        var allocateCommandBuffers =
            (delegate* unmanaged<VkDevice, VkCommandBufferAllocateInfo*, VkCommandBuffer*, VkResult>)device.GetProcAddress(
                "vkAllocateCommandBuffers\0"u8);
        var createSemaphore =
            (delegate* unmanaged<VkDevice, VkSemaphoreCreateInfo*, void*, VkSemaphore*, VkResult>)device.GetProcAddress(
                "vkCreateSemaphore\0"u8);
        _destroySemaphore =
            (delegate* unmanaged<VkDevice, VkSemaphore, void*, void>)device.GetProcAddress(
                "vkDestroySemaphore\0"u8);
        VkSemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = VkStructureType.SemaphoreCreateInfo
        };

        _frames = new FrameResources[FramesInFlight];
        for (int index = 0; index < _frames.Length; index++)
        {
            VkCommandPoolCreateInfo poolInfo = new()
            {
                SType = VkStructureType.CommandPoolCreateInfo,
                Flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
                QueueFamilyIndex = device.GraphicsQueueFamilyIndex
            };
            VkCommandPool commandPool = default;
            VulkanException.ThrowIfFailed(
                createCommandPool(
                    device.Handle,
                    &poolInfo,
                    null,
                    &commandPool),
                $"vkCreateCommandPool(frame[{index}])");

            VkCommandBufferAllocateInfo allocationInfo = new()
            {
                SType = VkStructureType.CommandBufferAllocateInfo,
                CommandPool = commandPool,
                Level = VkCommandBufferLevel.Primary,
                CommandBufferCount = 1
            };
            VkCommandBuffer commandBuffer = default;
            VulkanException.ThrowIfFailed(
                allocateCommandBuffers(
                    device.Handle,
                    &allocationInfo,
                    &commandBuffer),
                $"vkAllocateCommandBuffers(frame[{index}])");

            VkSemaphore imageAvailable = default;
            VulkanException.ThrowIfFailed(
                createSemaphore(
                    device.Handle,
                    &semaphoreInfo,
                    null,
                    &imageAvailable),
                $"vkCreateSemaphore(imageAvailable[{index}])");
            _frames[index] = new(
                commandPool,
                commandBuffer,
                imageAvailable);
        }

        _imageTimelineValues = new ulong[swapchain.Images.Count];
        for (int index = 0; index < _renderFinished.Length; index++)
        {
            VkSemaphore renderFinished = default;
            VulkanException.ThrowIfFailed(
                createSemaphore(
                    device.Handle,
                    &semaphoreInfo,
                    null,
                    &renderFinished),
                $"vkCreateSemaphore(renderFinished[{index}])");
            _renderFinished[index] = renderFinished;
        }
        _acquireNextImage =
            (delegate* unmanaged<VkDevice, VkSwapchainKHR, ulong, VkSemaphore, VkFence, uint*, VkResult>)device.GetProcAddress(
                "vkAcquireNextImageKHR\0"u8);
        _resetCommandBuffer =
            (delegate* unmanaged<VkCommandBuffer, uint, VkResult>)device.GetProcAddress(
                "vkResetCommandBuffer\0"u8);
        _beginCommandBuffer =
            (delegate* unmanaged<VkCommandBuffer, VkCommandBufferBeginInfo*, VkResult>)device.GetProcAddress(
                "vkBeginCommandBuffer\0"u8);
        _endCommandBuffer =
            (delegate* unmanaged<VkCommandBuffer, VkResult>)device.GetProcAddress(
                "vkEndCommandBuffer\0"u8);
        _cmdPipelineBarrier2 =
            (delegate* unmanaged<VkCommandBuffer, VkDependencyInfo*, void>)device.GetProcAddress(
                "vkCmdPipelineBarrier2\0"u8);
        _cmdBeginRendering =
            (delegate* unmanaged<VkCommandBuffer, VkRenderingInfo*, void>)device.GetProcAddress(
                "vkCmdBeginRendering\0"u8);
        _cmdEndRendering =
            (delegate* unmanaged<VkCommandBuffer, void>)device.GetProcAddress(
                "vkCmdEndRendering\0"u8);
        _queueSubmit2 =
            (delegate* unmanaged<VkQueue, uint, VkSubmitInfo2*, VkFence, VkResult>)device.GetProcAddress(
                "vkQueueSubmit2\0"u8);
        _queuePresent =
            (delegate* unmanaged<VkQueue, VkPresentInfoKhr*, VkResult>)device.GetProcAddress(
                "vkQueuePresentKHR\0"u8);
        _renderGraph = BuildFrameGraph(
            swapchain,
            trianglePipeline is not null);
        _graphSink = new(this);
    }

    public void RenderFrame(
        float red,
        float green,
        float blue)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        FrameResources frame = _frames[_nextFrame];
        if (frame.LastTimelineValue > 0)
        {
            _timeline.Wait(frame.LastTimelineValue);
        }

        _retirementQueue.Collect(_timeline.CompletedValue);

        uint imageIndex = 0;
        VkResult acquireResult = _acquireNextImage(
            _device.Handle,
            _swapchain.Handle,
            ulong.MaxValue,
            frame.ImageAvailable,
            default,
            &imageIndex);
        VulkanException.ThrowIfFailedOrIncomplete(
            acquireResult,
            "vkAcquireNextImageKHR");

        ulong priorImageTimeline = _imageTimelineValues[imageIndex];
        if (priorImageTimeline > 0)
        {
            _timeline.Wait(priorImageTimeline);
        }

        VulkanException.ThrowIfFailed(
            _resetCommandBuffer(frame.CommandBuffer, 0),
            "vkResetCommandBuffer");

        VkCommandBufferBeginInfo beginInfo = new()
        {
            SType = VkStructureType.CommandBufferBeginInfo,
            Flags = VkCommandBufferUsageFlags.OneTimeSubmit
        };
        VulkanException.ThrowIfFailed(
            _beginCommandBuffer(frame.CommandBuffer, &beginInfo),
            "vkBeginCommandBuffer");

        _graphSink.BeginFrame(
            frame.CommandBuffer,
            imageIndex,
            red,
            green,
            blue);
        _renderGraph.Execute(_graphSink);
        _graphSink.EndFrame();

        VulkanException.ThrowIfFailed(
            _endCommandBuffer(frame.CommandBuffer),
            "vkEndCommandBuffer");

        VkSemaphoreSubmitInfo waitInfo = new()
        {
            SType = VkStructureType.SemaphoreSubmitInfo,
            Semaphore = frame.ImageAvailable,
            StageMask = VkPipelineStageFlags2.ColorAttachmentOutput
        };
        VkCommandBufferSubmitInfo commandInfo = new()
        {
            SType = VkStructureType.CommandBufferSubmitInfo,
            CommandBuffer = frame.CommandBuffer,
            DeviceMask = 1
        };
        VkSemaphore renderFinished = _renderFinished[imageIndex];
        ulong timelineValue = _timeline.ReserveSignalValue();
        VkSemaphoreSubmitInfo* signalInfos = stackalloc VkSemaphoreSubmitInfo[2];
        signalInfos[0] = new()
        {
            SType = VkStructureType.SemaphoreSubmitInfo,
            Semaphore = renderFinished,
            StageMask = VkPipelineStageFlags2.AllCommands
        };
        signalInfos[1] = new()
        {
            SType = VkStructureType.SemaphoreSubmitInfo,
            Semaphore = _timeline.Handle,
            Value = timelineValue,
            StageMask = VkPipelineStageFlags2.AllCommands
        };
        VkSubmitInfo2 submitInfo = new()
        {
            SType = VkStructureType.SubmitInfo2,
            WaitSemaphoreInfoCount = 1,
            PWaitSemaphoreInfos = &waitInfo,
            CommandBufferInfoCount = 1,
            PCommandBufferInfos = &commandInfo,
            SignalSemaphoreInfoCount = 2,
            PSignalSemaphoreInfos = signalInfos
        };
        VulkanException.ThrowIfFailed(
            _queueSubmit2(
                _device.GraphicsQueue,
                1,
                &submitInfo,
                default),
            "vkQueueSubmit2");
        LastSubmittedTimelineValue = timelineValue;
        frame.LastTimelineValue = timelineValue;
        _imageTimelineValues[imageIndex] = timelineValue;
        _nextFrame = (_nextFrame + 1) % _frames.Length;

        VkSwapchainKHR swapchain = _swapchain.Handle;
        VkPresentInfoKhr presentInfo = new()
        {
            SType = VkStructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &renderFinished,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex
        };
        VkResult presentResult = _queuePresent(
            _device.GraphicsQueue,
            &presentInfo);
        VulkanException.ThrowIfFailedOrIncomplete(
            presentResult,
            "vkQueuePresentKHR");

        _initializedImages[imageIndex] = true;
    }

    public void RetireAfterCurrentFrame(Action release)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _retirementQueue.Retire(LastSubmittedTimelineValue, release);
    }

    public int CollectRetired()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _retirementQueue.Collect(_timeline.CompletedValue);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _device.WaitIdle();
        _retirementQueue.Collect(ulong.MaxValue);

        for (int index = 0; index < _renderFinished.Length; index++)
        {
            VkSemaphore renderFinished = _renderFinished[index];
            if (!renderFinished.IsNull)
            {
                _destroySemaphore(
                    _device.Handle,
                    renderFinished,
                    null);
                _renderFinished[index] = default;
            }
        }

        foreach (FrameResources frame in _frames)
        {
            if (!frame.ImageAvailable.IsNull)
            {
                _destroySemaphore(
                    _device.Handle,
                    frame.ImageAvailable,
                    null);
                frame.ImageAvailable = default;
            }

            if (!frame.CommandPool.IsNull)
            {
                _destroyCommandPool(
                    _device.Handle,
                    frame.CommandPool,
                    null);
                frame.CommandPool = default;
                frame.CommandBuffer = default;
            }
        }

        foreach (VkImageView view in _imageViews)
        {
            if (!view.IsNull)
            {
                _destroyImageView(
                    _device.Handle,
                    view,
                    null);
            }
        }

        _timeline.Dispose();
    }

    private void ApplyBarrier(
        VkCommandBuffer commandBuffer,
        uint imageIndex,
        in RenderBarrier planned)
    {
        if (!string.Equals(
                planned.Resource,
                "Swapchain.Color",
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"The P1 Vulkan graph sink cannot resolve resource '{planned.Resource}'.");
        }

        bool firstUse = planned.SourcePass == "<external>"
                        && !_initializedImages[imageIndex];
        VkImageMemoryBarrier2 barrier = new()
        {
            SType = VkStructureType.ImageMemoryBarrier2,
            SrcStageMask = firstUse
                ? VkPipelineStageFlags2.None
                : MapStage(planned.SourceStage),
            SrcAccessMask = firstUse
                ? VkAccessFlags2.None
                : MapAccess(planned.SourceAccess),
            DstStageMask = MapStage(planned.DestinationStage),
            DstAccessMask = MapAccess(planned.DestinationAccess),
            OldLayout = firstUse
                ? VkImageLayout.Undefined
                : MapLayout(planned.OldLayout),
            NewLayout = MapLayout(planned.NewLayout),
            SrcQueueFamilyIndex = QueueFamilyIgnored,
            DstQueueFamilyIndex = QueueFamilyIgnored,
            Image = _swapchain.Images[(int)imageIndex],
            SubresourceRange = ColorSubresourceRange()
        };
        VkDependencyInfo dependency = new()
        {
            SType = VkStructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier
        };
        _cmdPipelineBarrier2(commandBuffer, &dependency);
    }

    private void BeginRendering(
        VkCommandBuffer commandBuffer,
        uint imageIndex,
        LoadOp load,
        StoreOp store,
        float red,
        float green,
        float blue)
    {
        VkRenderingAttachmentInfo attachment = new()
        {
            SType = VkStructureType.RenderingAttachmentInfo,
            ImageView = _imageViews[imageIndex],
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = load switch
            {
                LoadOp.Load => VkAttachmentLoadOp.Load,
                LoadOp.Clear => VkAttachmentLoadOp.Clear,
                LoadOp.DontCare => VkAttachmentLoadOp.DontCare,
                _ => throw new ArgumentOutOfRangeException(nameof(load))
            },
            StoreOp = store switch
            {
                StoreOp.Store => VkAttachmentStoreOp.Store,
                StoreOp.DontCare => VkAttachmentStoreOp.DontCare,
                _ => throw new ArgumentOutOfRangeException(nameof(store))
            }
        };
        attachment.ClearValue.Float32[0] = red;
        attachment.ClearValue.Float32[1] = green;
        attachment.ClearValue.Float32[2] = blue;
        attachment.ClearValue.Float32[3] = 1.0f;

        VkRenderingInfo renderingInfo = new()
        {
            SType = VkStructureType.RenderingInfo,
            RenderArea = new()
            {
                Extent = _swapchain.Extent
            },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &attachment
        };
        _cmdBeginRendering(commandBuffer, &renderingInfo);
    }

    private static CompiledRenderGraph BuildFrameGraph(
        VulkanSwapchain swapchain,
        bool drawTriangle)
    {
        RenderGraphBuilder graph = new();
        RenderImage<ColorTarget> color = graph.ImportImage<ColorTarget>(
            "Swapchain.Color",
            new(
                ToPixelFormat(swapchain.SurfaceFormat.Format),
                ResourceSize2D.Absolute(
                    swapchain.Extent.Width,
                    swapchain.Extent.Height),
                ImageUsage.ColorAttachment | ImageUsage.Present,
                Transient: false),
            ImageAccess.Present());

        graph.Pass<FramePassData>("Frame.Clear", pass =>
        {
            pass.Write(
                color,
                ImageAccess.ColorAttachment(
                    LoadOp.Clear,
                    StoreOp.Store));
            pass.Execute(static (
                ref RenderGraphCommandContext _,
                in FramePassData _) =>
            {
            });
        });

        if (drawTriangle)
        {
            graph.Pass<FramePassData>("Frame.Triangle", pass =>
            {
                pass.ReadWrite(
                    color,
                    ImageAccess.ColorAttachment(
                        LoadOp.Load,
                        StoreOp.Store));
                pass.Execute(static (
                    ref RenderGraphCommandContext commands,
                    in FramePassData _) =>
                    commands
                        .GetBackend<IVulkanRenderGraphCommands>()
                        .DrawTriangle());
            });
        }

        graph.Pass<FramePassData>("Frame.Present", pass =>
        {
            pass.Read(color, ImageAccess.Present());
            pass.SideEffect();
            pass.Execute(static (
                ref RenderGraphCommandContext _,
                in FramePassData _) =>
            {
            });
        });
        return graph.Compile();
    }

    private static PixelFormat ToPixelFormat(VkFormat format) =>
        format switch
        {
            VkFormat.B8G8R8A8Srgb => PixelFormat.Bgra8Srgb,
            _ => throw new NotSupportedException(
                $"The P1 frame graph does not map swapchain format {format}.")
        };

    private static VkPipelineStageFlags2 MapStage(RenderPipelineStage stage)
    {
        VkPipelineStageFlags2 result = VkPipelineStageFlags2.None;
        Add(RenderPipelineStage.Top, VkPipelineStageFlags2.TopOfPipe);
        Add(RenderPipelineStage.DrawIndirect, VkPipelineStageFlags2.DrawIndirect);
        Add(RenderPipelineStage.VertexInput, VkPipelineStageFlags2.VertexInput);
        Add(RenderPipelineStage.VertexShader, VkPipelineStageFlags2.VertexShader);
        Add(RenderPipelineStage.FragmentShader, VkPipelineStageFlags2.FragmentShader);
        Add(RenderPipelineStage.EarlyDepthStencil, VkPipelineStageFlags2.EarlyFragmentTests);
        Add(RenderPipelineStage.LateDepthStencil, VkPipelineStageFlags2.LateFragmentTests);
        Add(RenderPipelineStage.ColorAttachmentOutput, VkPipelineStageFlags2.ColorAttachmentOutput);
        Add(RenderPipelineStage.ComputeShader, VkPipelineStageFlags2.ComputeShader);
        Add(RenderPipelineStage.Transfer, VkPipelineStageFlags2.Transfer);
        Add(RenderPipelineStage.Bottom, VkPipelineStageFlags2.BottomOfPipe);
        Add(RenderPipelineStage.Host, VkPipelineStageFlags2.Host);
        Add(RenderPipelineStage.AllGraphics, VkPipelineStageFlags2.AllGraphics);
        Add(RenderPipelineStage.AllCommands, VkPipelineStageFlags2.AllCommands);
        return result;

        void Add(RenderPipelineStage source, VkPipelineStageFlags2 destination)
        {
            if ((stage & source) != 0)
            {
                result |= destination;
            }
        }
    }

    private static VkAccessFlags2 MapAccess(RenderAccessFlags access)
    {
        VkAccessFlags2 result = VkAccessFlags2.None;
        Add(RenderAccessFlags.IndirectCommandRead, VkAccessFlags2.IndirectCommandRead);
        Add(RenderAccessFlags.IndexRead, VkAccessFlags2.IndexRead);
        Add(RenderAccessFlags.VertexAttributeRead, VkAccessFlags2.VertexAttributeRead);
        Add(RenderAccessFlags.UniformRead, VkAccessFlags2.UniformRead);
        Add(RenderAccessFlags.ShaderRead, VkAccessFlags2.ShaderRead);
        Add(RenderAccessFlags.ShaderWrite, VkAccessFlags2.ShaderWrite);
        Add(RenderAccessFlags.ColorAttachmentRead, VkAccessFlags2.ColorAttachmentRead);
        Add(RenderAccessFlags.ColorAttachmentWrite, VkAccessFlags2.ColorAttachmentWrite);
        Add(RenderAccessFlags.DepthStencilRead, VkAccessFlags2.DepthStencilRead);
        Add(RenderAccessFlags.DepthStencilWrite, VkAccessFlags2.DepthStencilWrite);
        Add(RenderAccessFlags.TransferRead, VkAccessFlags2.TransferRead);
        Add(RenderAccessFlags.TransferWrite, VkAccessFlags2.TransferWrite);
        Add(RenderAccessFlags.HostRead, VkAccessFlags2.HostRead);
        Add(RenderAccessFlags.HostWrite, VkAccessFlags2.HostWrite);
        Add(RenderAccessFlags.MemoryRead, VkAccessFlags2.MemoryRead);
        Add(RenderAccessFlags.MemoryWrite, VkAccessFlags2.MemoryWrite);
        return result;

        void Add(RenderAccessFlags source, VkAccessFlags2 destination)
        {
            if ((access & source) != 0)
            {
                result |= destination;
            }
        }
    }

    private static VkImageLayout MapLayout(RenderImageLayout? layout) =>
        layout switch
        {
            RenderImageLayout.Undefined => VkImageLayout.Undefined,
            RenderImageLayout.General => VkImageLayout.General,
            RenderImageLayout.ColorAttachment => VkImageLayout.ColorAttachmentOptimal,
            RenderImageLayout.DepthStencilAttachment => VkImageLayout.DepthStencilAttachmentOptimal,
            RenderImageLayout.DepthStencilReadOnly => VkImageLayout.DepthStencilReadOnlyOptimal,
            RenderImageLayout.ShaderReadOnly => VkImageLayout.ShaderReadOnlyOptimal,
            RenderImageLayout.TransferSource => VkImageLayout.TransferSourceOptimal,
            RenderImageLayout.TransferDestination => VkImageLayout.TransferDestinationOptimal,
            RenderImageLayout.Present => VkImageLayout.PresentSourceKhr,
            null => VkImageLayout.Undefined,
            _ => throw new ArgumentOutOfRangeException(nameof(layout))
        };

    private sealed class FrameResources(
        VkCommandPool commandPool,
        VkCommandBuffer commandBuffer,
        VkSemaphore imageAvailable)
    {
        public VkCommandPool CommandPool = commandPool;

        public VkCommandBuffer CommandBuffer = commandBuffer;

        public VkSemaphore ImageAvailable = imageAvailable;

        public ulong LastTimelineValue;
    }

    private readonly struct FramePassData;

    private sealed class FrameGraphSink(VulkanClearRenderer renderer)
        : IRenderGraphCommandSink, IVulkanRenderGraphCommands
    {
        private readonly VulkanClearRenderer _renderer = renderer;
        private VkCommandBuffer _commandBuffer;
        private uint _imageIndex;
        private float _red;
        private float _green;
        private float _blue;
        private bool _rendering;

        public void BeginFrame(
            VkCommandBuffer commandBuffer,
            uint imageIndex,
            float red,
            float green,
            float blue)
        {
            _commandBuffer = commandBuffer;
            _imageIndex = imageIndex;
            _red = red;
            _green = green;
            _blue = blue;
            _rendering = false;
        }

        public void EndFrame()
        {
            if (_rendering)
            {
                throw new InvalidOperationException(
                    "A Vulkan dynamic-rendering scope remained open after graph execution.");
            }

            _commandBuffer = default;
        }

        public void ApplyBarrier(in RenderBarrier barrier) =>
            _renderer.ApplyBarrier(
                _commandBuffer,
                _imageIndex,
                in barrier);

        public void BeginPass(CompiledRenderPass pass)
        {
            for (int index = 0; index < pass.Accesses.Count; index++)
            {
                CompiledResourceAccess access = pass.Accesses[index];
                if (access is
                    {
                        Resource: "Swapchain.Color",
                        Layout: RenderImageLayout.ColorAttachment
                    })
                {
                    _renderer.BeginRendering(
                        _commandBuffer,
                        _imageIndex,
                        access.Load,
                        access.Store,
                        _red,
                        _green,
                        _blue);
                    _rendering = true;
                    return;
                }
            }
        }

        public void DrawTriangle()
        {
            if (!_rendering)
            {
                throw new InvalidOperationException(
                    "DrawTriangle requires an active dynamic-rendering scope.");
            }

            VulkanTrianglePipeline pipeline = _renderer._trianglePipeline
                ?? throw new InvalidOperationException(
                    "The frame graph requested a triangle without a pipeline.");
            pipeline.Draw(
                _commandBuffer,
                _renderer._swapchain.Extent);
        }

        public void Command(string passName, string command) =>
            throw new NotSupportedException(
                $"String render command '{command}' from '{passName}' is not supported by the Vulkan sink.");

        public void EndPass(CompiledRenderPass pass)
        {
            if (_rendering)
            {
                _renderer._cmdEndRendering(_commandBuffer);
                _rendering = false;
            }
        }
    }

    private static VkImageSubresourceRange ColorSubresourceRange() =>
        new()
        {
            AspectMask = VkImageAspectFlags.Color,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1
        };
}
