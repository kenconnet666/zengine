using System.Runtime.InteropServices;

namespace ZEngine.Vulkan.Raw;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceProperties2Buffer
{
    public VkStructureType SType;
    public void* PNext;
    public fixed byte Properties[4096];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceDescriptorHeapPropertiesExt
{
    public VkStructureType SType;
    public void* PNext;
    public ulong SamplerHeapAlignment;
    public ulong ResourceHeapAlignment;
    public ulong MaxSamplerHeapSize;
    public ulong MaxResourceHeapSize;
    public ulong MinSamplerHeapReservedRange;
    public ulong MinSamplerHeapReservedRangeWithEmbedded;
    public ulong MinResourceHeapReservedRange;
    public ulong SamplerDescriptorSize;
    public ulong ImageDescriptorSize;
    public ulong BufferDescriptorSize;
    public ulong SamplerDescriptorAlignment;
    public ulong ImageDescriptorAlignment;
    public ulong BufferDescriptorAlignment;
    public ulong MaxPushDataSize;
    public nuint ImageCaptureReplayOpaqueDataSize;
    public uint MaxDescriptorHeapEmbeddedSamplers;
    public uint SamplerYcbcrConversionCount;
    public uint SparseDescriptorHeaps;
    public uint ProtectedDescriptorHeaps;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkDeviceAddressRangeExt
{
    public ulong Address;
    public ulong Size;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkBindHeapInfoExt
{
    public VkStructureType SType;
    public void* PNext;
    public VkDeviceAddressRangeExt HeapRange;
    public ulong ReservedRangeOffset;
    public ulong ReservedRangeSize;
}

public sealed record VulkanDescriptorHeapProperties(
    ulong ResourceHeapAlignment,
    ulong MaxResourceHeapSize,
    ulong MinResourceHeapReservedRange,
    ulong BufferDescriptorSize,
    ulong BufferDescriptorAlignment);
