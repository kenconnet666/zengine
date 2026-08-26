namespace ZEngine.Vulkan.Raw;

public sealed record VulkanInstanceOptions(
    VulkanApiVersion ApiVersion,
    IReadOnlyList<string> Extensions)
{
    public static VulkanInstanceOptions Default { get; } =
        new(VulkanApiVersion.Create(1, 4, 0), []);

    public static VulkanInstanceOptions Win32Surface { get; } =
        new(
            VulkanApiVersion.Create(1, 4, 0),
            ["VK_KHR_surface", "VK_KHR_win32_surface"]);
}
