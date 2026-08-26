using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

[Flags]
public enum VkMemoryPropertyFlags : uint
{
    None = 0,
    DeviceLocal = 0x00000001,
    HostVisible = 0x00000002,
    HostCoherent = 0x00000004,
    HostCached = 0x00000008,
    LazilyAllocated = 0x00000010,
    Protected = 0x00000020
}

[Flags]
public enum VkMemoryHeapFlags : uint
{
    None = 0,
    DeviceLocal = 0x00000001,
    MultiInstance = 0x00000002
}

[Flags]
public enum VkBufferUsageFlags : uint
{
    None = 0,
    TransferSource = 0x00000001,
    TransferDestination = 0x00000002,
    UniformTexelBuffer = 0x00000004,
    StorageTexelBuffer = 0x00000008,
    UniformBuffer = 0x00000010,
    StorageBuffer = 0x00000020,
    IndexBuffer = 0x00000040,
    VertexBuffer = 0x00000080,
    IndirectBuffer = 0x00000100,
    ShaderDeviceAddress = 0x00020000,
    DescriptorHeapExt = 0x10000000
}

[Flags]
public enum VkMemoryAllocateFlags : uint
{
    None = 0,
    DeviceMask = 0x00000001,
    DeviceAddress = 0x00000002,
    DeviceAddressCaptureReplay = 0x00000004
}

[StructLayout(LayoutKind.Sequential)]
public struct VkMemoryType
{
    public VkMemoryPropertyFlags PropertyFlags;
    public uint HeapIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkMemoryHeap
{
    public ulong Size;
    public VkMemoryHeapFlags Flags;
}

[InlineArray(32)]
public struct VkMemoryTypeArray
{
    private VkMemoryType _element0;
}

[InlineArray(16)]
public struct VkMemoryHeapArray
{
    private VkMemoryHeap _element0;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkPhysicalDeviceMemoryProperties
{
    public uint MemoryTypeCount;
    public VkMemoryTypeArray MemoryTypes;
    public uint MemoryHeapCount;
    public VkMemoryHeapArray MemoryHeaps;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkBufferCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public ulong Size;
    public VkBufferUsageFlags Usage;
    public VkSharingMode SharingMode;
    public uint QueueFamilyIndexCount;
    public uint* PQueueFamilyIndices;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkMemoryRequirements
{
    public ulong Size;
    public ulong Alignment;
    public uint MemoryTypeBits;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkMemoryAllocateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public ulong AllocationSize;
    public uint MemoryTypeIndex;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkMemoryAllocateFlagsInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkMemoryAllocateFlags Flags;
    public uint DeviceMask;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkBufferDeviceAddressInfo
{
    public VkStructureType SType;
    public void* PNext;
    public VkBuffer Buffer;
}

public static class VulkanHandleConversions
{
    public static nint ToBackendHandle(this VkDeviceMemory memory) =>
        unchecked((nint)memory.Value);

    public static VkDeviceMemory ToDeviceMemory(this nint handle) =>
        new(unchecked((ulong)handle));
}
