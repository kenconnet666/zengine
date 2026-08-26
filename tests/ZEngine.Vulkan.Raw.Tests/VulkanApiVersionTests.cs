namespace ZEngine.Vulkan.Raw.Tests;

using ZEngine.Platform.Win32;
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

    [Fact]
    public void CreatesHiddenWin32SurfaceAndSwapchain()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using Win32Window window = Win32Window.Create(
            "ZEngine Swapchain Test",
            640,
            360);
        using VulkanLoader loader = VulkanLoader.OpenSystem();
        using VulkanInstance instance = VulkanInstance.Create(
            loader,
            VulkanInstanceOptions.Win32Surface);
        VulkanPhysicalDeviceInfo physicalDevice =
            Assert.Single(instance.EnumeratePhysicalDevices());
        using VulkanSurface surface = instance.CreateWin32Surface(
            window.NativeInstance,
            window.NativeHandle);

        Assert.True(surface.SupportsPresentation(physicalDevice));

        using VulkanDevice device = instance.CreateGraphicsDevice(
            physicalDevice,
            enableSwapchain: true);
        using VulkanSwapchain swapchain = surface.CreateSwapchain(
            device,
            physicalDevice,
            window.ClientSize.Width,
            window.ClientSize.Height);

        Assert.False(surface.Handle.IsNull);
        Assert.False(swapchain.Handle.IsNull);
        Assert.True(swapchain.Images.Count >= 2);
        Assert.True(swapchain.Extent.Width > 0);
        Assert.True(swapchain.Extent.Height > 0);
        device.WaitIdle();

        Assert.DoesNotContain(
            instance.ValidationMessages,
            message =>
                (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0);
    }
}
