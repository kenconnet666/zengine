using ZEngine.Core;
using ZEngine.Platform;
using ZEngine.Vulkan.Raw;

PlatformEnvironment platform = PlatformEnvironment.Current;

Console.WriteLine($"ZEngine {EngineVersion.Current}");
Console.WriteLine(
    $"Platform: {platform.Platform}, {platform.ProcessArchitecture}, {platform.RuntimeIdentifier}");

try
{
    using VulkanLoader loader = VulkanLoader.OpenSystem();
    VulkanApiVersion version = loader.EnumerateInstanceVersion();
    Console.WriteLine($"Vulkan loader API: {version}");

    using VulkanInstance instance = VulkanInstance.Create(loader);
    IReadOnlyList<VulkanPhysicalDeviceInfo> physicalDevices =
        instance.EnumeratePhysicalDevices();

    foreach (VulkanPhysicalDeviceInfo physicalDevice in physicalDevices)
    {
        Console.WriteLine(
            $"GPU: {physicalDevice.Name}, Vulkan {physicalDevice.ApiVersion}, "
            + $"vendor=0x{physicalDevice.VendorId:X4}, device=0x{physicalDevice.DeviceId:X4}, "
            + $"type={physicalDevice.DeviceType}, graphicsQueue={physicalDevice.GraphicsQueueFamilyIndex}");
    }

    VulkanPhysicalDeviceInfo selected = physicalDevices
        .OrderByDescending(device => device.DeviceType == VkPhysicalDeviceType.DiscreteGpu)
        .FirstOrDefault()
        ?? throw new InvalidOperationException("No Vulkan physical device was found.");

    using VulkanDevice device = instance.CreateGraphicsDevice(selected);
    Console.WriteLine(
        $"Logical device created with graphics queue family {device.GraphicsQueueFamilyIndex}.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Vulkan probe failed: {exception.Message}");
    return 1;
}
