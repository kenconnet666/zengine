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
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Vulkan probe failed: {exception.Message}");
    return 1;
}
