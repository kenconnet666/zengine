using System.Runtime.InteropServices;
using ZEngine.Graphics.Vulkan;
using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.UI.Vulkan;

internal sealed unsafe class VulkanUiOverlay : IVulkanOverlay
{
    private readonly VulkanDevice _device;
    private readonly VulkanUiScene _scene;
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
    private readonly delegate* unmanaged<VkCommandBuffer, VkPipelineLayout, VkShaderStageFlags, uint, uint, void*, void>
        _cmdPushConstants;
    private readonly delegate* unmanaged<VkCommandBuffer, uint, uint, uint, uint, void>
        _cmdDraw;
    private VkPipelineLayout _layout;
    private VkPipeline _pipeline;

    public VulkanUiOverlay(
        VulkanDevice device,
        VkFormat colorFormat,
        VulkanUiScene scene,
        ReadOnlySpan<byte> vertexShader,
        ReadOnlySpan<byte> fragmentShader)
    {
        _device = device;
        _scene = scene;
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
        _cmdPushConstants =
            (delegate* unmanaged<VkCommandBuffer, VkPipelineLayout, VkShaderStageFlags, uint, uint, void*, void>)device.GetProcAddress(
                "vkCmdPushConstants\0"u8);
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
            VkPushConstantRange pushRange = new()
            {
                StageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
                Size = (uint)sizeof(UiPushConstants)
            };
            VkPipelineLayoutCreateInfo layoutInfo = new()
            {
                SType = VkStructureType.PipelineLayoutCreateInfo,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushRange
            };
            VkPipelineLayout layout = default;
            VulkanException.ThrowIfFailed(
                createPipelineLayout(
                    device.Handle,
                    &layoutInfo,
                    null,
                    &layout),
                "vkCreatePipelineLayout(ui)");
            _layout = layout;
            CreatePipeline(
                device,
                createGraphicsPipelines,
                colorFormat,
                vertexModule,
                fragmentModule);
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            if (!fragmentModule.IsNull)
            {
                destroyShaderModule(device.Handle, fragmentModule, null);
            }

            destroyShaderModule(device.Handle, vertexModule, null);
        }
    }

    public bool HasContent => _scene.Quads.Count > 0;

    public void Draw(VkCommandBuffer commandBuffer, VkExtent2D extent)
    {
        UiQuad[] quads = _scene.Quads as UiQuad[]
            ?? _scene.Quads.ToArray();
        if (quads.Length == 0)
        {
            return;
        }

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
        VkRect2D scissor = new() { Extent = extent };
        _cmdSetScissor(commandBuffer, 0, 1, &scissor);
        foreach (UiQuad quad in quads)
        {
            UiPushConstants push = new(quad, extent);
            _cmdPushConstants(
                commandBuffer,
                _layout,
                VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
                0,
                (uint)sizeof(UiPushConstants),
                &push);
            _cmdDraw(commandBuffer, 6, 1, 0, 0);
        }
    }

    public void Dispose()
    {
        VkPipeline pipeline = _pipeline;
        if (!pipeline.IsNull)
        {
            _pipeline = default;
            _destroyPipeline(_device.Handle, pipeline, null);
        }

        VkPipelineLayout layout = _layout;
        if (!layout.IsNull)
        {
            _layout = default;
            _destroyPipelineLayout(_device.Handle, layout, null);
        }
    }

    private void CreatePipeline(
        VulkanDevice device,
        delegate* unmanaged<VkDevice, VkPipelineCache, uint, VkGraphicsPipelineCreateInfo*, void*, VkPipeline*, VkResult>
            createGraphicsPipelines,
        VkFormat colorFormat,
        VkShaderModule vertexModule,
        VkShaderModule fragmentModule)
    {
        ReadOnlySpan<byte> vertexEntry = "VSMain\0"u8;
        ReadOnlySpan<byte> fragmentEntry = "PSMain\0"u8;
        fixed (byte* vertexName = vertexEntry)
        fixed (byte* fragmentName = fragmentEntry)
        {
            VkPipelineShaderStageCreateInfo* stages =
                stackalloc VkPipelineShaderStageCreateInfo[2];
            stages[0] = new()
            {
                SType = VkStructureType.PipelineShaderStageCreateInfo,
                Stage = VkShaderStageFlags.Vertex,
                Module = vertexModule,
                PName = vertexName
            };
            stages[1] = new()
            {
                SType = VkStructureType.PipelineShaderStageCreateInfo,
                Stage = VkShaderStageFlags.Fragment,
                Module = fragmentModule,
                PName = fragmentName
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
                LineWidth = 1
            };
            VkPipelineMultisampleStateCreateInfo multisample = new()
            {
                SType = VkStructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = VkSampleCountFlags.Count1
            };
            VkPipelineColorBlendAttachmentState blendAttachment = new()
            {
                BlendEnable = 1,
                SrcColorBlendFactor = VkBlendFactor.SourceAlpha,
                DstColorBlendFactor = VkBlendFactor.OneMinusSourceAlpha,
                ColorBlendOp = VkBlendOp.Add,
                SrcAlphaBlendFactor = VkBlendFactor.One,
                DstAlphaBlendFactor = VkBlendFactor.OneMinusSourceAlpha,
                AlphaBlendOp = VkBlendOp.Add,
                ColorWriteMask = VkColorComponentFlags.All
            };
            VkPipelineColorBlendStateCreateInfo blend = new()
            {
                SType = VkStructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = 1,
                PAttachments = &blendAttachment
            };
            VkDynamicState* states = stackalloc VkDynamicState[2];
            states[0] = VkDynamicState.Viewport;
            states[1] = VkDynamicState.Scissor;
            VkPipelineDynamicStateCreateInfo dynamic = new()
            {
                SType = VkStructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = states
            };
            VkFormat format = colorFormat;
            VkPipelineRenderingCreateInfo rendering = new()
            {
                SType = VkStructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 1,
                PColorAttachmentFormats = &format
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
                PColorBlendState = &blend,
                PDynamicState = &dynamic,
                Layout = _layout,
                BasePipelineIndex = -1
            };
            VkPipeline pipeline = default;
            VulkanException.ThrowIfFailed(
                createGraphicsPipelines(
                    device.Handle,
                    default,
                    1,
                    &pipelineInfo,
                    null,
                    &pipeline),
                "vkCreateGraphicsPipelines(ui)");
            _pipeline = pipeline;
        }
    }

    private static VkShaderModule CreateShaderModule(
        VulkanDevice device,
        delegate* unmanaged<VkDevice, VkShaderModuleCreateInfo*, void*, VkShaderModule*, VkResult>
            createShaderModule,
        ReadOnlySpan<byte> code)
    {
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
                "vkCreateShaderModule(ui)");
            return module;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UiPushConstants
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public float ViewportWidth;
        public float ViewportHeight;
        public float Padding0;
        public float Padding1;
        public float Red;
        public float Green;
        public float Blue;
        public float Alpha;

        public UiPushConstants(UiQuad quad, VkExtent2D extent)
        {
            X = quad.X;
            Y = quad.Y;
            Width = quad.Width;
            Height = quad.Height;
            ViewportWidth = extent.Width;
            ViewportHeight = extent.Height;
            Padding0 = 0;
            Padding1 = 0;
            Red = quad.Color.Red / 255f;
            Green = quad.Color.Green / 255f;
            Blue = quad.Color.Blue / 255f;
            Alpha = quad.Color.Alpha / 255f;
        }
    }
}
