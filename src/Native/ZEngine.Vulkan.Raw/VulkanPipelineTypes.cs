using System.Runtime.InteropServices;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

[Flags]
public enum VkShaderStageFlags : uint
{
    Vertex = 0x00000001,
    Fragment = 0x00000010
}

public enum VkPrimitiveTopology : int
{
    TriangleList = 3
}

public enum VkPolygonMode : int
{
    Fill = 0
}

[Flags]
public enum VkCullModeFlags : uint
{
    None = 0,
    Front = 0x00000001,
    Back = 0x00000002
}

public enum VkFrontFace : int
{
    CounterClockwise = 1,
    Clockwise = 0
}

[Flags]
public enum VkSampleCountFlags : uint
{
    Count1 = 0x00000001
}

[Flags]
public enum VkColorComponentFlags : uint
{
    R = 0x00000001,
    G = 0x00000002,
    B = 0x00000004,
    A = 0x00000008,
    All = R | G | B | A
}

public enum VkBlendFactor : int
{
    Zero = 0,
    One = 1,
    SourceAlpha = 6,
    OneMinusSourceAlpha = 7
}

public enum VkBlendOp : int
{
    Add = 0
}

public enum VkLogicOp : int
{
    Copy = 3
}

public enum VkDynamicState : int
{
    Viewport = 0,
    Scissor = 1
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkShaderModuleCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public nuint CodeSize;
    public uint* PCode;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineShaderStageCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public VkShaderStageFlags Stage;
    public VkShaderModule Module;
    public byte* PName;
    public void* PSpecializationInfo;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineVertexInputStateCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint VertexBindingDescriptionCount;
    public void* PVertexBindingDescriptions;
    public uint VertexAttributeDescriptionCount;
    public void* PVertexAttributeDescriptions;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineInputAssemblyStateCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public VkPrimitiveTopology Topology;
    public uint PrimitiveRestartEnable;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineViewportStateCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint ViewportCount;
    public VkViewport* PViewports;
    public uint ScissorCount;
    public VkRect2D* PScissors;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineRasterizationStateCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint DepthClampEnable;
    public uint RasterizerDiscardEnable;
    public VkPolygonMode PolygonMode;
    public VkCullModeFlags CullMode;
    public VkFrontFace FrontFace;
    public uint DepthBiasEnable;
    public float DepthBiasConstantFactor;
    public float DepthBiasClamp;
    public float DepthBiasSlopeFactor;
    public float LineWidth;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineMultisampleStateCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public VkSampleCountFlags RasterizationSamples;
    public uint SampleShadingEnable;
    public float MinSampleShading;
    public uint* PSampleMask;
    public uint AlphaToCoverageEnable;
    public uint AlphaToOneEnable;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkPipelineColorBlendAttachmentState
{
    public uint BlendEnable;
    public VkBlendFactor SrcColorBlendFactor;
    public VkBlendFactor DstColorBlendFactor;
    public VkBlendOp ColorBlendOp;
    public VkBlendFactor SrcAlphaBlendFactor;
    public VkBlendFactor DstAlphaBlendFactor;
    public VkBlendOp AlphaBlendOp;
    public VkColorComponentFlags ColorWriteMask;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineColorBlendStateCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint LogicOpEnable;
    public VkLogicOp LogicOp;
    public uint AttachmentCount;
    public VkPipelineColorBlendAttachmentState* PAttachments;
    public fixed float BlendConstants[4];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineDynamicStateCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint DynamicStateCount;
    public VkDynamicState* PDynamicStates;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineLayoutCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint SetLayoutCount;
    public VkDescriptorSetLayout* PSetLayouts;
    public uint PushConstantRangeCount;
    public void* PPushConstantRanges;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkPushConstantRange
{
    public VkShaderStageFlags StageFlags;
    public uint Offset;
    public uint Size;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineRenderingCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint ViewMask;
    public uint ColorAttachmentCount;
    public VkFormat* PColorAttachmentFormats;
    public VkFormat DepthAttachmentFormat;
    public VkFormat StencilAttachmentFormat;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkGraphicsPipelineCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint StageCount;
    public VkPipelineShaderStageCreateInfo* PStages;
    public VkPipelineVertexInputStateCreateInfo* PVertexInputState;
    public VkPipelineInputAssemblyStateCreateInfo* PInputAssemblyState;
    public void* PTessellationState;
    public VkPipelineViewportStateCreateInfo* PViewportState;
    public VkPipelineRasterizationStateCreateInfo* PRasterizationState;
    public VkPipelineMultisampleStateCreateInfo* PMultisampleState;
    public void* PDepthStencilState;
    public VkPipelineColorBlendStateCreateInfo* PColorBlendState;
    public VkPipelineDynamicStateCreateInfo* PDynamicState;
    public VkPipelineLayout Layout;
    public VkRenderPass RenderPass;
    public uint Subpass;
    public VkPipeline BasePipelineHandle;
    public int BasePipelineIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkViewport
{
    public float X;
    public float Y;
    public float Width;
    public float Height;
    public float MinDepth;
    public float MaxDepth;
}
