namespace ZEngine.Vulkan.Raw.Tests;

using ZEngine.Vulkan.Raw.Generated;
using Xunit;

public sealed class VulkanApiVersionTests
{
    [Fact]
    public void GeneratedRegistryCatalogMatchesPinnedSource()
    {
        Assert.Equal(new Version(1, 4), VulkanRegistryInfo.HighestCoreVersion);
        Assert.Equal(63, VulkanRegistryInfo.HandleCount);
        Assert.Equal(865, VulkanRegistryInfo.CommandCount);
    }

    [Fact]
    public void EncodesAndDecodesVersion()
    {
        VulkanApiVersion version = VulkanApiVersion.Create(1, 4, 349);

        Assert.Equal(0u, version.Variant);
        Assert.Equal(1u, version.Major);
        Assert.Equal(4u, version.Minor);
        Assert.Equal(349u, version.Patch);
        Assert.Equal("1.4.349", version.ToString());
    }

    [Fact]
    public void OpensWindowsSystemLoader()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using VulkanLoader loader = VulkanLoader.OpenSystem();
        VulkanApiVersion version = loader.EnumerateInstanceVersion();

        Assert.True(version.Major >= 1);
        Assert.True(version.Minor >= 4);
    }

    [Fact]
    public void CreatesInstanceEnumeratesGpuAndCreatesLogicalDevice()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using VulkanLoader loader = VulkanLoader.OpenSystem();
        using VulkanInstance instance = VulkanInstance.Create(loader);

        IReadOnlyList<VulkanPhysicalDeviceInfo> devices =
            instance.EnumeratePhysicalDevices();

        VulkanPhysicalDeviceInfo selected = Assert.Single(devices);
        Assert.False(string.IsNullOrWhiteSpace(selected.Name));
        Assert.True(selected.ApiVersion.Major >= 1);
        Assert.True(selected.Extensions.Contains("VK_KHR_swapchain"));

        using VulkanDevice device =
            instance.CreateGraphicsDevice(selected);

        Assert.False(device.Handle.IsNull);
        Assert.False(device.GraphicsQueue.IsNull);
    }
}
