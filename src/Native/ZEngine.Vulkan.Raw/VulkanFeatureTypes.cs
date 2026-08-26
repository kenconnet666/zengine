using System.Runtime.InteropServices;

namespace ZEngine.Vulkan.Raw;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceFeatures
{
    public fixed uint Values[55];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceFeatures2
{
    public VkStructureType SType;
    public void* PNext;
    public VkPhysicalDeviceFeatures Features;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceBufferDeviceAddressFeatures
{
    public VkStructureType SType;
    public void* PNext;
    public uint BufferDeviceAddress;
    public uint BufferDeviceAddressCaptureReplay;
    public uint BufferDeviceAddressMultiDevice;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceDynamicRenderingLocalReadFeatures
{
    public VkStructureType SType;
    public void* PNext;
    public uint DynamicRenderingLocalRead;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceDescriptorBufferFeaturesExt
{
    public VkStructureType SType;
    public void* PNext;
    public uint DescriptorBuffer;
    public uint DescriptorBufferCaptureReplay;
    public uint DescriptorBufferImageLayoutIgnored;
    public uint DescriptorBufferPushDescriptors;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceDescriptorHeapFeaturesExt
{
    public VkStructureType SType;
    public void* PNext;
    public uint DescriptorHeap;
    public uint DescriptorHeapCaptureReplay;
}

public enum VulkanDescriptorBindingModel
{
    DescriptorSets,
    DescriptorBuffer,
    DescriptorHeap
}

public sealed record VulkanDeviceCapabilities(
    bool DynamicRenderingLocalRead,
    bool DescriptorBuffer,
    bool DescriptorHeap,
    bool BufferDeviceAddress,
    VulkanDescriptorBindingModel DescriptorBindingModel,
    VulkanDescriptorHeapProperties? DescriptorHeapProperties);
