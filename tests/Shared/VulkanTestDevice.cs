using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Testing;

internal static class VulkanTestDevice
{
    public static VulkanPhysicalDeviceInfo Select(
        VulkanInstance instance,
        Func<VulkanPhysicalDeviceInfo, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        IReadOnlyList<VulkanPhysicalDeviceInfo> devices =
            instance.EnumeratePhysicalDevices();
        IEnumerable<VulkanPhysicalDeviceInfo> candidates = predicate is null
            ? devices
            : devices.Where(predicate);
        VulkanPhysicalDeviceInfo? selected = candidates
            .OrderByDescending(device => Rank(device.DeviceType))
            .ThenByDescending(device => device.ApiVersion.Value)
            .ThenByDescending(device => device.Extensions.Count)
            .ThenBy(device => device.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        return selected ?? throw new InvalidOperationException(
            "No compatible Vulkan physical device was found. Available devices: "
            + string.Join(", ", devices.Select(device => device.Name)));
    }

    private static int Rank(VkPhysicalDeviceType deviceType) =>
        deviceType switch
        {
            VkPhysicalDeviceType.DiscreteGpu => 4,
            VkPhysicalDeviceType.IntegratedGpu => 3,
            VkPhysicalDeviceType.VirtualGpu => 2,
            VkPhysicalDeviceType.Cpu => 1,
            _ => 0
        };
}
