using ZEngine.Core;
using ZEngine.Graphics.Vulkan;
using ZEngine.Platform;
using ZEngine.Platform.Win32;
using ZEngine.Vulkan.Raw;
using System.Globalization;

PlatformEnvironment platform = PlatformEnvironment.Current;

Console.WriteLine($"ZEngine {EngineVersion.Current}");
Console.WriteLine(
    $"Platform: {platform.Platform}, {platform.ProcessArchitecture}, {platform.RuntimeIdentifier}");

try
{
    bool windowSmoke = args.Contains(
        "--window-smoke",
        StringComparer.Ordinal);
    double windowSeconds = 2;
    string? durationArgument = args.FirstOrDefault(
        argument => argument.StartsWith(
            "--window-seconds=",
            StringComparison.Ordinal));
    if (durationArgument is not null
        && (!double.TryParse(
                durationArgument["--window-seconds=".Length..],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out windowSeconds)
            || windowSeconds <= 0))
    {
        throw new ArgumentException(
            $"Invalid window smoke duration: '{durationArgument}'.");
    }

    using VulkanLoader loader = VulkanLoader.OpenSystem();
    VulkanApiVersion version = loader.EnumerateInstanceVersion();
    Console.WriteLine($"Vulkan loader API: {version}");

    using VulkanInstance instance = VulkanInstance.Create(
        loader,
        windowSmoke
            ? VulkanInstanceOptions.Win32Surface
            : VulkanInstanceOptions.Default);
    IReadOnlyList<VulkanPhysicalDeviceInfo> physicalDevices =
        instance.EnumeratePhysicalDevices();

    foreach (VulkanPhysicalDeviceInfo physicalDevice in physicalDevices)
    {
        Console.WriteLine(
            $"GPU: {physicalDevice.Name}, Vulkan {physicalDevice.ApiVersion}, "
            + $"vendor=0x{physicalDevice.VendorId:X4}, device=0x{physicalDevice.DeviceId:X4}, "
            + $"type={physicalDevice.DeviceType}, graphicsQueue={physicalDevice.GraphicsQueueFamilyIndex}");
    }

    VulkanPhysicalDeviceInfo selected = physicalDevices
        .OrderByDescending(device => device.DeviceType == VkPhysicalDeviceType.DiscreteGpu)
        .FirstOrDefault()
        ?? throw new InvalidOperationException("No Vulkan physical device was found.");

    if (!windowSmoke)
    {
        using VulkanDevice device = instance.CreateGraphicsDevice(selected);
        Console.WriteLine(
            $"Logical device created with graphics queue family {device.GraphicsQueueFamilyIndex}.");
    }
    else
    {
        using Win32Window window = Win32Window.Create(
            "ZEngine Vulkan 1.4 Smoke",
            960,
            540,
            visible: true);
        using VulkanSurface surface = instance.CreateWin32Surface(
            window.NativeInstance,
            window.NativeHandle);

        if (!surface.SupportsPresentation(selected))
        {
            throw new InvalidOperationException(
                "The selected graphics queue cannot present to the Win32 surface.");
        }

        using VulkanDevice device = instance.CreateGraphicsDevice(
            selected,
            enableSwapchain: true);
        using VulkanSwapchain swapchain = surface.CreateSwapchain(
            device,
            selected,
            window.ClientSize.Width,
            window.ClientSize.Height);

        Console.WriteLine(
            $"Swapchain created: {swapchain.Extent.Width}x{swapchain.Extent.Height}, "
            + $"{swapchain.SurfaceFormat.Format}, {swapchain.PresentMode}, "
            + $"{swapchain.Images.Count} images.");

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
        using VulkanTrianglePipeline trianglePipeline = new(
            device,
            swapchain.SurfaceFormat.Format,
            vertexShader,
            fragmentShader);
        using VulkanClearRenderer renderer = new(
            device,
            swapchain,
            trianglePipeline);
        DateTime deadline = DateTime.UtcNow.AddSeconds(windowSeconds);
        float phase = 0;
        while (DateTime.UtcNow < deadline && window.PumpEvents())
        {
            float red = 0.08f + 0.04f * MathF.Sin(phase);
            float blue = 0.14f + 0.05f * MathF.Cos(phase);
            renderer.RenderFrame(red, 0.10f, blue);
            phase += 0.04f;
            Thread.Sleep(16);
        }
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Vulkan probe failed: {exception.Message}");
    return 1;
}
