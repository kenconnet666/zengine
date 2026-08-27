using ZEngine.Platform.Win32;
using ZEngine.RenderGraph;
using ZEngine.Testing;
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
        using VulkanClearRenderer renderer = new(device, swapchain);

        Assert.Equal(
            ["Frame.Clear", "Frame.Present"],
            renderer.FrameGraph.Passes.Select(pass => pass.Name));
        Assert.Contains(
            renderer.FrameGraph.Barriers,
            barrier =>
                barrier.SourcePass == "<external>"
                && barrier.DestinationPass == "Frame.Clear"
                && barrier.NewLayout == RenderImageLayout.ColorAttachment);
        Assert.Contains(
            renderer.FrameGraph.Barriers,
            barrier =>
                barrier.SourcePass == "Frame.Clear"
                && barrier.DestinationPass == "Frame.Present"
                && barrier.NewLayout == RenderImageLayout.Present);
        Assert.Equal(2, renderer.FrameResourceCount);

        renderer.RenderFrame(0.08f, 0.1f, 0.18f);
        int stressRetired = 0;
        for (int resource = 0; resource < 1_000; resource++)
        {
            renderer.RetireAfterCurrentFrame(() => stressRetired++);
        }

        for (int frame = 1; frame < 8; frame++)
        {
            renderer.RenderFrame(
                0.08f + frame * 0.002f,
                0.1f,
                0.18f);
        }
        Assert.Equal(1_000, stressRetired);
        device.WaitIdle();

        Assert.True(renderer.LastSubmittedTimelineValue >= 8);
        Assert.True(
            renderer.CompletedTimelineValue
            >= renderer.LastSubmittedTimelineValue);
        bool retired = false;
        renderer.RetireAfterCurrentFrame(() => retired = true);
        Assert.Equal(1, renderer.CollectRetired());
        Assert.True(retired);

        Assert.DoesNotContain(
            instance.ValidationMessages,
            message =>
                (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0);
    }
}
