using ZEngine.Platform.Win32;
using ZEngine.Vulkan.Raw;
using Xunit;

namespace ZEngine.Graphics.Vulkan.Tests;

public sealed class VulkanClearRendererTests
{
    [Fact]
    public void ClearsAndPresentsHiddenWin32Swapchain()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using Win32Window window = Win32Window.Create(
            "ZEngine Clear Test",
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
        using VulkanDevice device = instance.CreateGraphicsDevice(
            physicalDevice,
            enableSwapchain: true);
        using VulkanSwapchain swapchain = surface.CreateSwapchain(
            device,
            physicalDevice,
            window.ClientSize.Width,
            window.ClientSize.Height);
        using VulkanClearRenderer renderer = new(device, swapchain);

        renderer.RenderFrame(0.08f, 0.1f, 0.18f);
        renderer.RenderFrame(0.1f, 0.12f, 0.2f);
    }
}
