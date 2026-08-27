using ZEngine.Graphics.Vulkan;
using ZEngine.Platform;
using ZEngine.Platform.Win32;
using ZEngine.Testing;
using ZEngine.Vulkan.Raw;
using Xunit;

namespace ZEngine.Graphics.Vulkan.Tests;

public sealed class VulkanPipelineReloadTests
{
    [Fact]
    public void AppliesValidPipelineAndKeepsCurrentGenerationOnFailure()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using Win32Window window = Win32Window.Create(
            "ZEngine Pipeline Reload Test",
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

        byte[] vertexShader = ReadShader("triangle.vert.spv");
        byte[] fragmentShader = ReadShader("triangle.frag.spv");
        byte[] alternateFragmentShader = ReadShader("triangle-alt.frag.spv");
        using VulkanWindowRenderer renderer = new(
            device,
            physicalDevice,
            surface,
            vertexShader,
            fragmentShader);
        WindowSize size = window.ClientSize;

        Assert.True(renderer.RenderFrame(
            size.Width,
            size.Height,
            suspended: false,
            0.04f,
            0.05f,
            0.08f));
        Assert.Equal(1u, renderer.ShaderGeneration);

        ShaderReloadResult unchanged = renderer.ReloadShaders(
            vertexShader,
            fragmentShader);
        Assert.Equal(ShaderReloadStatus.Unchanged, unchanged.Status);
        Assert.Equal(1u, unchanged.Generation);

        ShaderReloadResult applied = renderer.ReloadShaders(
            vertexShader,
            alternateFragmentShader);
        Assert.Equal(ShaderReloadStatus.Applied, applied.Status);
        Assert.Equal(2u, applied.Generation);
        Assert.True(renderer.RenderFrame(
            size.Width,
            size.Height,
            suspended: false,
            0.05f,
            0.06f,
            0.09f));

        ShaderReloadResult rejected = renderer.ReloadShaders(
            vertexShader,
            [0x01, 0x02, 0x03, 0x04]);
        Assert.Equal(ShaderReloadStatus.Rejected, rejected.Status);
        Assert.Equal(2u, rejected.Generation);
        Assert.False(string.IsNullOrWhiteSpace(rejected.Error));
        Assert.True(renderer.RenderFrame(
            size.Width,
            size.Height,
            suspended: false,
            0.06f,
            0.07f,
            0.1f));

        byte[] pipelineCacheData = renderer.GetPipelineCacheData();
        Assert.NotEmpty(pipelineCacheData);
        using VulkanPipelineCache importedCache = new(
            device,
            pipelineCacheData);
        Assert.NotEmpty(importedCache.Snapshot());

        device.WaitIdle();
        Assert.DoesNotContain(
            instance.ValidationMessages,
            message =>
                (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0);
    }

    private static byte[] ReadShader(string name) =>
        File.ReadAllBytes(
            Path.Combine(
                AppContext.BaseDirectory,
                "Shaders",
                name));
}
