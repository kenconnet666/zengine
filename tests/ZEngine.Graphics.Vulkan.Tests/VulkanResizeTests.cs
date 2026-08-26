using ZEngine.Graphics.Vulkan;
using ZEngine.Platform;
using ZEngine.Platform.Win32;
using ZEngine.Vulkan.Raw;
using Xunit;

namespace ZEngine.Graphics.Vulkan.Tests;

public sealed class VulkanResizeTests
{
    [Fact]
    public void RebuildsSwapchainAcrossResizeMinimizeAndRestore()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using Win32Window window = Win32Window.Create(
            "ZEngine Resize Test",
            640,
            360,
            visible: true);
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

        byte[] vertexShader = File.ReadAllBytes(
            Path.Combine(
                AppContext.BaseDirectory,
                "Shaders",
                "triangle.vert.spv"));
        byte[] fragmentShader = File.ReadAllBytes(
            Path.Combine(
                AppContext.BaseDirectory,
                "Shaders",
                "triangle.frag.spv"));
        using VulkanWindowRenderer renderer = new(
            device,
            physicalDevice,
            surface,
            vertexShader,
            fragmentShader);

        WindowSize initial = window.ClientSize;
        Assert.True(renderer.RenderFrame(
            initial.Width,
            initial.Height,
            window.IsMinimized,
            0.04f,
            0.05f,
            0.08f));
        Assert.Equal(1u, renderer.Generation);

        window.ResizeClient(800, 450);
        WaitUntil(
            window,
            () => window.ClientSize == new WindowSize(800, 450));
        WindowSize resized = window.ClientSize;
        Assert.True(renderer.RenderFrame(
            resized.Width,
            resized.Height,
            window.IsMinimized,
            0.05f,
            0.06f,
            0.09f));
        Assert.Equal(2u, renderer.Generation);
        Assert.Equal(800u, renderer.Extent?.Width);
        Assert.Equal(450u, renderer.Extent?.Height);

        window.Minimize();
        WaitUntil(window, () => window.IsMinimized);
        Assert.False(renderer.RenderFrame(
            resized.Width,
            resized.Height,
            window.IsMinimized,
            0.05f,
            0.06f,
            0.09f));
        Assert.Equal(2u, renderer.Generation);

        window.Restore();
        window.ResizeClient(720, 405);
        WaitUntil(
            window,
            () =>
                !window.IsMinimized
                && window.ClientSize == new WindowSize(720, 405));
        WindowSize restored = window.ClientSize;
        Assert.True(renderer.RenderFrame(
            restored.Width,
            restored.Height,
            window.IsMinimized,
            0.06f,
            0.07f,
            0.1f));
        Assert.Equal(3u, renderer.Generation);
        Assert.Equal(720u, renderer.Extent?.Width);
        Assert.Equal(405u, renderer.Extent?.Height);

        device.WaitIdle();
        Assert.DoesNotContain(
            instance.ValidationMessages,
            message =>
                (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0);
    }

    private static void WaitUntil(
        Win32Window window,
        Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            Assert.True(window.PumpEvents());
            if (condition())
            {
                return;
            }

            Thread.Sleep(10);
        }

        Assert.Fail("The expected Win32 window state was not observed before timeout.");
    }
}
