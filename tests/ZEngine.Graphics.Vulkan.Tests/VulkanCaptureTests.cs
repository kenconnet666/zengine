using System.Buffers.Binary;
using ZEngine.Graphics.Vulkan;
using ZEngine.Platform.Win32;
using ZEngine.Testing;
using ZEngine.Vulkan.Raw;
using Xunit;

namespace ZEngine.Graphics.Vulkan.Tests;

public sealed class VulkanCaptureTests
{
    [Fact]
    public void CapturesSwapchainThroughTransferPassAndEncodesPng()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using Win32Window window = Win32Window.Create(
            "ZEngine Capture Test",
            320,
            180);
        using VulkanLoader loader = VulkanLoader.OpenSystem();
        using VulkanInstance instance = VulkanInstance.Create(
            loader,
            VulkanInstanceOptions.Win32Surface);
        using VulkanSurface surface = instance.CreateWin32Surface(
            window.NativeInstance,
            window.NativeHandle);
        VulkanPhysicalDeviceInfo physicalDevice = VulkanTestDevice.Select(
            instance,
            surface.SupportsPresentation);
        using VulkanDevice device = instance.CreateGraphicsDevice(
            physicalDevice,
            enableSwapchain: true);
        using VulkanWindowRenderer renderer = new(
            device,
            physicalDevice,
            surface,
            ReadShader("triangle.vert.spv"),
            ReadShader("triangle.frag.spv"),
            captureFactory: new VulkanFrameCaptureFactory());
        var size = window.ClientSize;
        Assert.True(renderer.RenderFrame(
            size.Width,
            size.Height,
            suspended: false,
            0.03f,
            0.04f,
            0.07f));

        byte[] png = renderer.CapturePng();

        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            png.AsSpan(0, 8).ToArray());
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(size.Width, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(size.Height, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
        Assert.True(png.Length > 100);
        Assert.DoesNotContain(
            instance.ValidationMessages,
            message =>
                (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0);
    }

    private static byte[] ReadShader(string name) =>
        File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Shaders", name));
}
