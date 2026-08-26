using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public sealed unsafe class VulkanTrianglePipeline : IDisposable
{
    private readonly VulkanDevice _device;
    private readonly delegate* unmanaged<VkDevice, VkPipeline, void*, void>
        _destroyPipeline;
    private readonly delegate* unmanaged<VkDevice, VkPipelineLayout, void*, void>
        _destroyPipelineLayout;
    private readonly delegate* unmanaged<VkCommandBuffer, VkPipelineBindPoint, VkPipeline, void>
        _cmdBindPipeline;
    private readonly delegate* unmanaged<VkCommandBuffer, uint, uint, VkViewport*, void>
        _cmdSetViewport;
    private readonly delegate* unmanaged<VkCommandBuffer, uint, uint, VkRect2D*, void>
        _cmdSetScissor;
    private readonly delegate* unmanaged<VkCommandBuffer, uint, uint, uint, uint, void>
        _cmdDraw;
    private VkPipelineLayout _layout;
    private VkPipeline _pipeline;

    public VulkanTrianglePipeline(
        VulkanDevice device,
        VkFormat colorFormat,
        ReadOnlySpan<byte> vertexShader,
        ReadOnlySpan<byte> fragmentShader,
        VulkanPipelineCache? pipelineCache = null)
    {
        _device = device;

        var createShaderModule =
            (delegate* unmanaged<VkDevice, VkShaderModuleCreateInfo*, void*, VkShaderModule*, VkResult>)device.GetProcAddress(
                "vkCreateShaderModule\0"u8);
        var destroyShaderModule =
            (delegate* unmanaged<VkDevice, VkShaderModule, void*, void>)device.GetProcAddress(
                "vkDestroyShaderModule\0"u8);
        var createPipelineLayout =
            (delegate* unmanaged<VkDevice, VkPipelineLayoutCreateInfo*, void*, VkPipelineLayout*, VkResult>)device.GetProcAddress(
                "vkCreatePipelineLayout\0"u8);
        _destroyPipelineLayout =
            (delegate* unmanaged<VkDevice, VkPipelineLayout, void*, void>)device.GetProcAddress(
                "vkDestroyPipelineLayout\0"u8);
        var createGraphicsPipelines =
            (delegate* unmanaged<VkDevice, VkPipelineCache, uint, VkGraphicsPipelineCreateInfo*, void*, VkPipeline*, VkResult>)device.GetProcAddress(
                "vkCreateGraphicsPipelines\0"u8);
        _destroyPipeline =
            (delegate* unmanaged<VkDevice, VkPipeline, void*, void>)device.GetProcAddress(
                "vkDestroyPipeline\0"u8);

        _cmdBindPipeline =
            (delegate* unmanaged<VkCommandBuffer, VkPipelineBindPoint, VkPipeline, void>)device.GetProcAddress(
                "vkCmdBindPipeline\0"u8);
        _cmdSetViewport =
            (delegate* unmanaged<VkCommandBuffer, uint, uint, VkViewport*, void>)device.GetProcAddress(
                "vkCmdSetViewport\0"u8);
        _cmdSetScissor =
            (delegate* unmanaged<VkCommandBuffer, uint, uint, VkRect2D*, void>)device.GetProcAddress(
                "vkCmdSetScissor\0"u8);
        _cmdDraw =
            (delegate* unmanaged<VkCommandBuffer, uint, uint, uint, uint, void>)device.GetProcAddress(
                "vkCmdDraw\0"u8);

        VkShaderModule vertexModule = CreateShaderModule(
            device,
            createShaderModule,
            vertexShader);
        VkShaderModule fragmentModule = default;

        try
        {
            fragmentModule = CreateShaderModule(
                device,
                createShaderModule,
                fragmentShader);
            VkPipelineLayoutCreateInfo layoutInfo = new()
            {
                SType = VkStructureType.PipelineLayoutCreateInfo
            };
            VkPipelineLayout layout = default;
            VulkanException.ThrowIfFailed(
                createPipelineLayout(
                    device.Handle,
                    &layoutInfo,
                    null,
                    &layout),
                "vkCreatePipelineLayout");
            _layout = layout;

            ReadOnlySpan<byte> vertexEntry = "VSMain\0"u8;
            ReadOnlySpan<byte> fragmentEntry = "PSMain\0"u8;
            fixed (byte* vertexEntryPointer = vertexEntry)
            fixed (byte* fragmentEntryPointer = fragmentEntry)
            {
                VkPipelineShaderStageCreateInfo* stages =
                    stackalloc VkPipelineShaderStageCreateInfo[2];
                stages[0] = new()
                {
                    SType = VkStructureType.PipelineShaderStageCreateInfo,
                    Stage = VkShaderStageFlags.Vertex,
                    Module = vertexModule,
                    PName = vertexEntryPointer
                };
                stages[1] = new()
                {
                    SType = VkStructureType.PipelineShaderStageCreateInfo,
                    Stage = VkShaderStageFlags.Fragment,
                    Module = fragmentModule,
                    PName = fragmentEntryPointer
                };

                VkPipelineVertexInputStateCreateInfo vertexInput = new()
                {
                    SType = VkStructureType.PipelineVertexInputStateCreateInfo
                };
                VkPipelineInputAssemblyStateCreateInfo inputAssembly = new()
                {
                    SType = VkStructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = VkPrimitiveTopology.TriangleList
                };
                VkPipelineViewportStateCreateInfo viewportState = new()
                {
                    SType = VkStructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    ScissorCount = 1
                };
                VkPipelineRasterizationStateCreateInfo rasterization = new()
                {
                    SType = VkStructureType.PipelineRasterizationStateCreateInfo,
                    PolygonMode = VkPolygonMode.Fill,
                    CullMode = VkCullModeFlags.None,
                    FrontFace = VkFrontFace.CounterClockwise,
                    LineWidth = 1.0f
                };
                VkPipelineMultisampleStateCreateInfo multisample = new()
                {
                    SType = VkStructureType.PipelineMultisampleStateCreateInfo,
                    RasterizationSamples = VkSampleCountFlags.Count1
                };
                VkPipelineColorBlendAttachmentState blendAttachment = new()
                {
                    SrcColorBlendFactor = VkBlendFactor.One,
                    DstColorBlendFactor = VkBlendFactor.Zero,
                    ColorBlendOp = VkBlendOp.Add,
                    SrcAlphaBlendFactor = VkBlendFactor.One,
                    DstAlphaBlendFactor = VkBlendFactor.Zero,
                    AlphaBlendOp = VkBlendOp.Add,
                    ColorWriteMask = VkColorComponentFlags.All
                };
                VkPipelineColorBlendStateCreateInfo colorBlend = new()
                {
                    SType = VkStructureType.PipelineColorBlendStateCreateInfo,
                    LogicOp = VkLogicOp.Copy,
                    AttachmentCount = 1,
                    PAttachments = &blendAttachment
                };
                VkDynamicState* dynamicStates = stackalloc VkDynamicState[2];
                dynamicStates[0] = VkDynamicState.Viewport;
                dynamicStates[1] = VkDynamicState.Scissor;
                VkPipelineDynamicStateCreateInfo dynamicState = new()
                {
                    SType = VkStructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = 2,
                    PDynamicStates = dynamicStates
                };
                VkFormat pipelineColorFormat = colorFormat;
                VkPipelineRenderingCreateInfo rendering = new()
                {
                    SType = VkStructureType.PipelineRenderingCreateInfo,
                    ColorAttachmentCount = 1,
                    PColorAttachmentFormats = &pipelineColorFormat
                };
                VkGraphicsPipelineCreateInfo pipelineInfo = new()
                {
                    SType = VkStructureType.GraphicsPipelineCreateInfo,
                    PNext = &rendering,
                    StageCount = 2,
                    PStages = stages,
                    PVertexInputState = &vertexInput,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterization,
                    PMultisampleState = &multisample,
                    PColorBlendState = &colorBlend,
                    PDynamicState = &dynamicState,
                    Layout = _layout,
                    BasePipelineIndex = -1
                };

                VkPipeline pipeline = default;
                VulkanException.ThrowIfFailed(
                    createGraphicsPipelines(
                        device.Handle,
                        pipelineCache?.Handle ?? default,
                        1,
                        &pipelineInfo,
                        null,
                        &pipeline),
                    "vkCreateGraphicsPipelines");
                _pipeline = pipeline;
            }
        }
        catch
        {
            DisposeAfterGpuCompletion();
            throw;
        }
        finally
        {
            if (!fragmentModule.IsNull)
            {
                destroyShaderModule(
                    device.Handle,
                    fragmentModule,
                    null);
            }

            destroyShaderModule(
                device.Handle,
                vertexModule,
                null);
        }
    }

    public VkPipeline Handle => _pipeline;

    public VkPipelineLayout Layout => _layout;

    public void Draw(
        VkCommandBuffer commandBuffer,
        VkExtent2D extent)
    {
        ObjectDisposedException.ThrowIf(_pipeline.IsNull, this);

        _cmdBindPipeline(
            commandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipeline);

        VkViewport viewport = new()
        {
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0,
            MaxDepth = 1
        };
        _cmdSetViewport(commandBuffer, 0, 1, &viewport);

        VkRect2D scissor = new()
        {
            Extent = extent
        };
        _cmdSetScissor(commandBuffer, 0, 1, &scissor);
        _cmdDraw(commandBuffer, 3, 1, 0, 0);
    }

    public void Dispose()
    {
        _device.WaitIdle();
        DisposeAfterGpuCompletion();
    }

    internal void DisposeAfterGpuCompletion()
    {
        VkPipeline pipeline = _pipeline;
        if (!pipeline.IsNull)
        {
            _pipeline = default;
            _destroyPipeline(
                _device.Handle,
                pipeline,
                null);
        }

        VkPipelineLayout layout = _layout;
        if (!layout.IsNull)
        {
            _layout = default;
            _destroyPipelineLayout(
                _device.Handle,
                layout,
                null);
        }
    }

    private static VkShaderModule CreateShaderModule(
        VulkanDevice device,
        delegate* unmanaged<VkDevice, VkShaderModuleCreateInfo*, void*, VkShaderModule*, VkResult>
            createShaderModule,
        ReadOnlySpan<byte> code)
    {
        if (code.IsEmpty || code.Length % sizeof(uint) != 0)
        {
            throw new ArgumentException(
                "SPIR-V bytecode must be non-empty and four-byte aligned.",
                nameof(code));
        }

        fixed (byte* codePointer = code)
        {
            VkShaderModuleCreateInfo createInfo = new()
            {
                SType = VkStructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)codePointer
            };
            VkShaderModule module = default;
            VulkanException.ThrowIfFailed(
                createShaderModule(
                    device.Handle,
                    &createInfo,
                    null,
                    &module),
                "vkCreateShaderModule");
            return module;
        }
    }
}
