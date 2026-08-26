using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public sealed unsafe class VulkanClearRenderer : IDisposable
{
    private const uint QueueFamilyIgnored = uint.MaxValue;
    private readonly VulkanDevice _device;
    private readonly VulkanSwapchain _swapchain;
    private readonly VkImageView[] _imageViews;
    private readonly bool[] _initializedImages;
    private readonly delegate* unmanaged<VkDevice, VkImageView, void*, void>
        _destroyImageView;
    private readonly delegate* unmanaged<VkDevice, VkCommandPool, void*, void>
        _destroyCommandPool;
    private readonly delegate* unmanaged<VkDevice, VkSemaphore, void*, void>
        _destroySemaphore;
    private readonly delegate* unmanaged<VkDevice, VkFence, void*, void>
        _destroyFence;
    private readonly delegate* unmanaged<VkDevice, uint, VkFence*, uint, ulong, VkResult>
        _waitForFences;
    private readonly delegate* unmanaged<VkDevice, uint, VkFence*, VkResult>
        _resetFences;
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
    private VkCommandPool _commandPool;
    private VkCommandBuffer _commandBuffer;
    private VkSemaphore _imageAvailable;
    private VkSemaphore _renderFinished;
    private VkFence _inFlightFence;
    private bool _disposed;

    public VulkanClearRenderer(
        VulkanDevice device,
        VulkanSwapchain swapchain)
    {
        _device = device;
        _swapchain = swapchain;

        var createImageView =
            (delegate* unmanaged<VkDevice, VkImageViewCreateInfo*, void*, VkImageView*, VkResult>)device.GetProcAddress(
                "vkCreateImageView\0"u8);
        _destroyImageView =
            (delegate* unmanaged<VkDevice, VkImageView, void*, void>)device.GetProcAddress(
                "vkDestroyImageView\0"u8);

        _imageViews = new VkImageView[swapchain.Images.Count];
        _initializedImages = new bool[swapchain.Images.Count];
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
            "vkCreateCommandPool");
        _commandPool = commandPool;

        var allocateCommandBuffers =
            (delegate* unmanaged<VkDevice, VkCommandBufferAllocateInfo*, VkCommandBuffer*, VkResult>)device.GetProcAddress(
                "vkAllocateCommandBuffers\0"u8);
        VkCommandBufferAllocateInfo allocationInfo = new()
        {
            SType = VkStructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = VkCommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        VkCommandBuffer commandBuffer = default;
        VulkanException.ThrowIfFailed(
            allocateCommandBuffers(
                device.Handle,
                &allocationInfo,
                &commandBuffer),
            "vkAllocateCommandBuffers");
        _commandBuffer = commandBuffer;

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
        VkSemaphore imageAvailable = default;
        VulkanException.ThrowIfFailed(
            createSemaphore(
                device.Handle,
                &semaphoreInfo,
                null,
                &imageAvailable),
            "vkCreateSemaphore(imageAvailable)");
        _imageAvailable = imageAvailable;

        VkSemaphore renderFinished = default;
        VulkanException.ThrowIfFailed(
            createSemaphore(
                device.Handle,
                &semaphoreInfo,
                null,
                &renderFinished),
            "vkCreateSemaphore(renderFinished)");
        _renderFinished = renderFinished;

        var createFence =
            (delegate* unmanaged<VkDevice, VkFenceCreateInfo*, void*, VkFence*, VkResult>)device.GetProcAddress(
                "vkCreateFence\0"u8);
        _destroyFence =
            (delegate* unmanaged<VkDevice, VkFence, void*, void>)device.GetProcAddress(
                "vkDestroyFence\0"u8);
        VkFenceCreateInfo fenceInfo = new()
        {
            SType = VkStructureType.FenceCreateInfo,
            Flags = VkFenceCreateFlags.Signaled
        };
        VkFence inFlightFence = default;
        VulkanException.ThrowIfFailed(
            createFence(
                device.Handle,
                &fenceInfo,
                null,
                &inFlightFence),
            "vkCreateFence");
        _inFlightFence = inFlightFence;

        _waitForFences =
            (delegate* unmanaged<VkDevice, uint, VkFence*, uint, ulong, VkResult>)device.GetProcAddress(
                "vkWaitForFences\0"u8);
        _resetFences =
            (delegate* unmanaged<VkDevice, uint, VkFence*, VkResult>)device.GetProcAddress(
                "vkResetFences\0"u8);
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
    }

    public void RenderFrame(
        float red,
        float green,
        float blue)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        VkFence fence = _inFlightFence;
        VulkanException.ThrowIfFailed(
            _waitForFences(
                _device.Handle,
                1,
                &fence,
                1,
                ulong.MaxValue),
            "vkWaitForFences");
        VulkanException.ThrowIfFailed(
            _resetFences(_device.Handle, 1, &fence),
            "vkResetFences");

        uint imageIndex = 0;
        VkResult acquireResult = _acquireNextImage(
            _device.Handle,
            _swapchain.Handle,
            ulong.MaxValue,
            _imageAvailable,
            default,
            &imageIndex);
        VulkanException.ThrowIfFailedOrIncomplete(
            acquireResult,
            "vkAcquireNextImageKHR");

        VulkanException.ThrowIfFailed(
            _resetCommandBuffer(_commandBuffer, 0),
            "vkResetCommandBuffer");

        VkCommandBufferBeginInfo beginInfo = new()
        {
            SType = VkStructureType.CommandBufferBeginInfo,
            Flags = VkCommandBufferUsageFlags.OneTimeSubmit
        };
        VulkanException.ThrowIfFailed(
            _beginCommandBuffer(_commandBuffer, &beginInfo),
            "vkBeginCommandBuffer");

        TransitionToColorAttachment(imageIndex);
        BeginClearRendering(imageIndex, red, green, blue);
        _cmdEndRendering(_commandBuffer);
        TransitionToPresent(imageIndex);

        VulkanException.ThrowIfFailed(
            _endCommandBuffer(_commandBuffer),
            "vkEndCommandBuffer");

        VkSemaphoreSubmitInfo waitInfo = new()
        {
            SType = VkStructureType.SemaphoreSubmitInfo,
            Semaphore = _imageAvailable,
            StageMask = VkPipelineStageFlags2.ColorAttachmentOutput
        };
        VkCommandBufferSubmitInfo commandInfo = new()
        {
            SType = VkStructureType.CommandBufferSubmitInfo,
            CommandBuffer = _commandBuffer,
            DeviceMask = 1
        };
        VkSemaphoreSubmitInfo signalInfo = new()
        {
            SType = VkStructureType.SemaphoreSubmitInfo,
            Semaphore = _renderFinished,
            StageMask = VkPipelineStageFlags2.AllCommands
        };
        VkSubmitInfo2 submitInfo = new()
        {
            SType = VkStructureType.SubmitInfo2,
            WaitSemaphoreInfoCount = 1,
            PWaitSemaphoreInfos = &waitInfo,
            CommandBufferInfoCount = 1,
            PCommandBufferInfos = &commandInfo,
            SignalSemaphoreInfoCount = 1,
            PSignalSemaphoreInfos = &signalInfo
        };
        VulkanException.ThrowIfFailed(
            _queueSubmit2(
                _device.GraphicsQueue,
                1,
                &submitInfo,
                _inFlightFence),
            "vkQueueSubmit2");

        VkSemaphore renderFinished = _renderFinished;
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _device.WaitIdle();

        if (!_inFlightFence.IsNull)
        {
            _destroyFence(
                _device.Handle,
                _inFlightFence,
                null);
            _inFlightFence = default;
        }

        if (!_renderFinished.IsNull)
        {
            _destroySemaphore(
                _device.Handle,
                _renderFinished,
                null);
            _renderFinished = default;
        }

        if (!_imageAvailable.IsNull)
        {
            _destroySemaphore(
                _device.Handle,
                _imageAvailable,
                null);
            _imageAvailable = default;
        }

        if (!_commandPool.IsNull)
        {
            _destroyCommandPool(
                _device.Handle,
                _commandPool,
                null);
            _commandPool = default;
            _commandBuffer = default;
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
    }

    private void TransitionToColorAttachment(uint imageIndex)
    {
        VkImageMemoryBarrier2 barrier = new()
        {
            SType = VkStructureType.ImageMemoryBarrier2,
            SrcStageMask = VkPipelineStageFlags2.None,
            SrcAccessMask = VkAccessFlags2.None,
            DstStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
            DstAccessMask =
                VkAccessFlags2.ColorAttachmentRead
                | VkAccessFlags2.ColorAttachmentWrite,
            OldLayout = _initializedImages[imageIndex]
                ? VkImageLayout.PresentSourceKhr
                : VkImageLayout.Undefined,
            NewLayout = VkImageLayout.ColorAttachmentOptimal,
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
        _cmdPipelineBarrier2(_commandBuffer, &dependency);
    }

    private void BeginClearRendering(
        uint imageIndex,
        float red,
        float green,
        float blue)
    {
        VkRenderingAttachmentInfo attachment = new()
        {
            SType = VkStructureType.RenderingAttachmentInfo,
            ImageView = _imageViews[imageIndex],
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store
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
        _cmdBeginRendering(_commandBuffer, &renderingInfo);
    }

    private void TransitionToPresent(uint imageIndex)
    {
        VkImageMemoryBarrier2 barrier = new()
        {
            SType = VkStructureType.ImageMemoryBarrier2,
            SrcStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
            SrcAccessMask = VkAccessFlags2.ColorAttachmentWrite,
            DstStageMask = VkPipelineStageFlags2.None,
            DstAccessMask = VkAccessFlags2.None,
            OldLayout = VkImageLayout.ColorAttachmentOptimal,
            NewLayout = VkImageLayout.PresentSourceKhr,
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
        _cmdPipelineBarrier2(_commandBuffer, &dependency);
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
