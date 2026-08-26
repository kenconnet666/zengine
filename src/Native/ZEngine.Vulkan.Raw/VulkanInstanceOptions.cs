namespace ZEngine.Vulkan.Raw;

public sealed record VulkanInstanceOptions(
    VulkanApiVersion ApiVersion,
    IReadOnlyList<string> Extensions,
    bool EnableDebugMessenger)
{
    public static VulkanInstanceOptions Default { get; } =
        new(VulkanApiVersion.Create(1, 4, 0), [], false);

    public static VulkanInstanceOptions Validation { get; } =
        new(
            VulkanApiVersion.Create(1, 4, 0),
            ["VK_EXT_debug_utils"],
            true);

    public static VulkanInstanceOptions Win32Surface { get; } =
        new(
            VulkanApiVersion.Create(1, 4, 0),
            [
                "VK_KHR_surface",
                "VK_KHR_win32_surface",
                "VK_EXT_debug_utils"
            ],
            true);
}
