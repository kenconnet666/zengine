using System.Runtime.InteropServices;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

public enum VkImageViewType : int
{
    Image2D = 1
}

public enum VkComponentSwizzle : int
{
    Identity = 0
}

public enum VkImageLayout : int
{
    Undefined = 0,
    General = 1,
    ColorAttachmentOptimal = 2,
    DepthStencilAttachmentOptimal = 3,
    DepthStencilReadOnlyOptimal = 4,
    ShaderReadOnlyOptimal = 5,
    TransferSourceOptimal = 6,
    TransferDestinationOptimal = 7,
    PresentSourceKhr = 1000001002
}

public enum VkAttachmentLoadOp : int
{
    Load = 0,
    Clear = 1,
    DontCare = 2
}

public enum VkAttachmentStoreOp : int
{
    Store = 0,
    DontCare = 1
}

public enum VkResolveModeFlags : uint
{
    None = 0
}

public enum VkCommandBufferLevel : int
{
    Primary = 0,
    Secondary = 1
}

public enum VkPipelineBindPoint : int
{
    Graphics = 0,
    Compute = 1
}

public enum VkSemaphoreType : int
{
    Binary = 0,
    Timeline = 1
}

[Flags]
public enum VkSemaphoreWaitFlags : uint
{
    None = 0,
    Any = 0x00000001
}

[Flags]
public enum VkImageAspectFlags : uint
{
    Color = 0x00000001,
    Depth = 0x00000002,
    Stencil = 0x00000004
}

[Flags]
public enum VkCommandPoolCreateFlags : uint
{
    Transient = 0x00000001,
    ResetCommandBuffer = 0x00000002
}

[Flags]
public enum VkCommandBufferUsageFlags : uint
{
    OneTimeSubmit = 0x00000001
}

[Flags]
public enum VkFenceCreateFlags : uint
{
    Signaled = 0x00000001
}

[Flags]
public enum VkPipelineStageFlags2 : ulong
{
    None = 0,
    TopOfPipe = 0x00000001,
    DrawIndirect = 0x00000002,
    VertexInput = 0x00000004,
    VertexShader = 0x00000008,
    FragmentShader = 0x00000080,
    EarlyFragmentTests = 0x00000100,
    LateFragmentTests = 0x00000200,
    ColorAttachmentOutput = 0x00000400,
    ComputeShader = 0x00000800,
    Transfer = 0x00001000,
    BottomOfPipe = 0x00002000,
    Host = 0x00004000,
    AllGraphics = 0x00008000,
    AllCommands = 0x00010000
}

[Flags]
public enum VkAccessFlags2 : ulong
{
    None = 0,
    IndirectCommandRead = 0x00000001,
    IndexRead = 0x00000002,
    VertexAttributeRead = 0x00000004,
    UniformRead = 0x00000008,
    ShaderRead = 0x00000020,
    ShaderWrite = 0x00000040,
    ColorAttachmentRead = 0x00000080,
    ColorAttachmentWrite = 0x00000100,
    DepthStencilRead = 0x00000200,
    DepthStencilWrite = 0x00000400,
    TransferRead = 0x00000800,
    TransferWrite = 0x00001000,
    HostRead = 0x00002000,
    HostWrite = 0x00004000,
    MemoryRead = 0x00008000,
    MemoryWrite = 0x00010000
}

[StructLayout(LayoutKind.Sequential)]
public struct VkComponentMapping
{
    public VkComponentSwizzle R;
    public VkComponentSwizzle G;
    public VkComponentSwizzle B;
    public VkComponentSwizzle A;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkImageSubresourceRange
{
    public VkImageAspectFlags AspectMask;
    public uint BaseMipLevel;
    public uint LevelCount;
    public uint BaseArrayLayer;
    public uint LayerCount;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkImageViewCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public VkImage Image;
    public VkImageViewType ViewType;
    public VkFormat Format;
    public VkComponentMapping Components;
    public VkImageSubresourceRange SubresourceRange;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkCommandPoolCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkCommandPoolCreateFlags Flags;
    public uint QueueFamilyIndex;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkCommandBufferAllocateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkCommandPool CommandPool;
    public VkCommandBufferLevel Level;
    public uint CommandBufferCount;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkCommandBufferBeginInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkCommandBufferUsageFlags Flags;
    public void* PInheritanceInfo;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkSemaphoreCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkSemaphoreTypeCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkSemaphoreType SemaphoreType;
    public ulong InitialValue;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkSemaphoreWaitInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkSemaphoreWaitFlags Flags;
    public uint SemaphoreCount;
    public VkSemaphore* PSemaphores;
    public ulong* PValues;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkSemaphoreSignalInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkSemaphore Semaphore;
    public ulong Value;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkFenceCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkFenceCreateFlags Flags;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkOffset2D
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkRect2D
{
    public VkOffset2D Offset;
    public VkExtent2D Extent;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
public unsafe struct VkClearValue
{
    [FieldOffset(0)]
    public fixed float Float32[4];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkRenderingAttachmentInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkImageView ImageView;
    public VkImageLayout ImageLayout;
    public VkResolveModeFlags ResolveMode;
    public VkImageView ResolveImageView;
    public VkImageLayout ResolveImageLayout;
    public VkAttachmentLoadOp LoadOp;
    public VkAttachmentStoreOp StoreOp;
    public VkClearValue ClearValue;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkRenderingInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public VkRect2D RenderArea;
    public uint LayerCount;
    public uint ViewMask;
    public uint ColorAttachmentCount;
    public VkRenderingAttachmentInfo* PColorAttachments;
    public VkRenderingAttachmentInfo* PDepthAttachment;
    public VkRenderingAttachmentInfo* PStencilAttachment;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkImageMemoryBarrier2
{
    public VkStructureType SType;
    public void* PNext;
    public VkPipelineStageFlags2 SrcStageMask;
    public VkAccessFlags2 SrcAccessMask;
    public VkPipelineStageFlags2 DstStageMask;
    public VkAccessFlags2 DstAccessMask;
    public VkImageLayout OldLayout;
    public VkImageLayout NewLayout;
    public uint SrcQueueFamilyIndex;
    public uint DstQueueFamilyIndex;
    public VkImage Image;
    public VkImageSubresourceRange SubresourceRange;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDependencyInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint DependencyFlags;
    public uint MemoryBarrierCount;
    public void* PMemoryBarriers;
    public uint BufferMemoryBarrierCount;
    public void* PBufferMemoryBarriers;
    public uint ImageMemoryBarrierCount;
    public VkImageMemoryBarrier2* PImageMemoryBarriers;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkSemaphoreSubmitInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkSemaphore Semaphore;
    public ulong Value;
    public VkPipelineStageFlags2 StageMask;
    public uint DeviceIndex;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkCommandBufferSubmitInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkCommandBuffer CommandBuffer;
    public uint DeviceMask;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkSubmitInfo2
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint WaitSemaphoreInfoCount;
    public VkSemaphoreSubmitInfo* PWaitSemaphoreInfos;
    public uint CommandBufferInfoCount;
    public VkCommandBufferSubmitInfo* PCommandBufferInfos;
    public uint SignalSemaphoreInfoCount;
    public VkSemaphoreSubmitInfo* PSignalSemaphoreInfos;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPresentInfoKhr
{
    public VkStructureType SType;
    public void* PNext;
    public uint WaitSemaphoreCount;
    public VkSemaphore* PWaitSemaphores;
    public uint SwapchainCount;
    public VkSwapchainKHR* PSwapchains;
    public uint* PImageIndices;
    public VkResult* PResults;
}
