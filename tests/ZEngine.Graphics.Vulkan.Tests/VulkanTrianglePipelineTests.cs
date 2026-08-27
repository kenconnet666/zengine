using ZEngine.Platform.Win32;
using ZEngine.RenderGraph;
using ZEngine.Testing;
using ZEngine.Vulkan.Raw;
using Xunit;

namespace ZEngine.Graphics.Vulkan.Tests;

public sealed class VulkanTrianglePipelineTests
{
    [Fact]
    public void CreatesPipelineDrawsAndPresentsTriangle()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using Win32Window window = Win32Window.Create(
            "ZEngine Triangle Test",
            640,
            360);
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
        using VulkanSwapchain swapchain = surface.CreateSwapchain(
            device,
            physicalDevice,
            window.ClientSize.Width,
            window.ClientSize.Height);

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

        using VulkanTrianglePipeline pipeline = new(
            device,
            swapchain.SurfaceFormat.Format,
            vertexShader,
            fragmentShader);
        using VulkanClearRenderer renderer = new(
            device,
            swapchain,
            pipeline);

        Assert.Equal(
            ["Frame.Clear", "Frame.Triangle", "Frame.Present"],
            renderer.FrameGraph.Passes.Select(pass => pass.Name));
        Assert.Contains(
            renderer.FrameGraph.Barriers,
            barrier =>
                barrier.SourcePass == "Frame.Clear"
                && barrier.DestinationPass == "Frame.Triangle"
                && barrier.OldLayout == RenderImageLayout.ColorAttachment
                && barrier.NewLayout == RenderImageLayout.ColorAttachment);
        Assert.Contains(
            renderer.FrameGraph.Barriers,
            barrier =>
                barrier.SourcePass == "Frame.Triangle"
                && barrier.DestinationPass == "Frame.Present"
                && barrier.NewLayout == RenderImageLayout.Present);
        Assert.True(renderer.DebugLabelsEnabled);

        for (int frame = 0; frame < 8; frame++)
        {
            renderer.RenderFrame(
                0.04f + frame * 0.002f,
                0.05f,
                0.08f);
        }
        device.WaitIdle();

        Assert.DoesNotContain(
            instance.ValidationMessages,
            message =>
                (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0);
    }
}
