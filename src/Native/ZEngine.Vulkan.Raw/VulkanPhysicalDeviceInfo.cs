using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

public sealed record VulkanPhysicalDeviceInfo(
    VkPhysicalDevice Handle,
    string Name,
    VulkanApiVersion ApiVersion,
    uint DriverVersion,
    uint VendorId,
    uint DeviceId,
    VkPhysicalDeviceType DeviceType,
    uint GraphicsQueueFamilyIndex,
    IReadOnlySet<string> Extensions);
