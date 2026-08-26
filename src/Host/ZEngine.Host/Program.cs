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
    bool resizeSmoke = args.Contains(
        "--resize-smoke",
        StringComparer.Ordinal);
    bool shaderReloadSmoke = args.Contains(
        "--shader-reload-smoke",
        StringComparer.Ordinal);
    windowSmoke |= resizeSmoke || shaderReloadSmoke;
    double windowSeconds = 2;
    string? durationArgument = args.FirstOrDefault(
        argument => argument.StartsWith(
            "--window-seconds=",
            StringComparison.Ordinal));
    if (resizeSmoke && durationArgument is null)
    {
        windowSeconds = 3.5;
    }
    else if (shaderReloadSmoke && durationArgument is null)
    {
        windowSeconds = 3;
    }

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
        PrintCapabilities(device.Capabilities);
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
        PrintCapabilities(device.Capabilities);
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
        byte[]? alternateFragmentShader = shaderReloadSmoke
            ? File.ReadAllBytes(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Shaders",
                    "triangle-alt.frag.spv"))
            : null;
        using VulkanWindowRenderer renderer = new(
            device,
            selected,
            surface,
            vertexShader,
            fragmentShader);
        WindowSize initialSize = window.ClientSize;
        renderer.Prepare(
            initialSize.Width,
            initialSize.Height,
            window.IsMinimized);
        VkExtent2D initialExtent = renderer.Extent
            ?? throw new InvalidOperationException(
                "The visible window did not produce a swapchain extent.");
        Console.WriteLine(
            $"Swapchain generation {renderer.Generation}: "
            + $"{initialExtent.Width}x{initialExtent.Height}.");

        DateTime deadline = DateTime.UtcNow.AddSeconds(windowSeconds);
        DateTime started = DateTime.UtcNow;
        float phase = 0;
        uint reportedGeneration = renderer.Generation;
        int resizePhase = 0;
        int shaderReloadPhase = 0;
        while (DateTime.UtcNow < deadline && window.PumpEvents())
        {
            double elapsed = (DateTime.UtcNow - started).TotalSeconds;
            if (resizeSmoke)
            {
                if (resizePhase == 0 && elapsed >= 0.5)
                {
                    window.ResizeClient(800, 450);
                    resizePhase = 1;
                    Console.WriteLine("Resize smoke: 800x450 requested.");
                }
                else if (resizePhase == 1 && elapsed >= 1.25)
                {
                    window.Minimize();
                    resizePhase = 2;
                    Console.WriteLine("Resize smoke: minimized.");
                }
                else if (resizePhase == 2 && elapsed >= 2.0)
                {
                    window.Restore();
                    window.ResizeClient(720, 405);
                    resizePhase = 3;
                    Console.WriteLine("Resize smoke: restored at 720x405.");
                }
            }

            if (shaderReloadSmoke
                && shaderReloadPhase == 0
                && elapsed >= 0.75)
            {
                ShaderReloadResult applied = renderer.ReloadShaders(
                    vertexShader,
                    alternateFragmentShader!);
                if (applied.Status != ShaderReloadStatus.Applied)
                {
                    throw new InvalidOperationException(
                        $"Valid shader reload was not applied: {applied.Error}");
                }

                shaderReloadPhase = 1;
                Console.WriteLine(
                    $"Shader reload: generation {applied.Generation} applied.");
            }
            else if (shaderReloadSmoke
                     && shaderReloadPhase == 1
                     && elapsed >= 1.5)
            {
                ShaderReloadResult rejected = renderer.ReloadShaders(
                    vertexShader,
                    [0x01, 0x02, 0x03, 0x04]);
                if (rejected.Status != ShaderReloadStatus.Rejected)
                {
                    throw new InvalidOperationException(
                        "Invalid shader reload was not rejected.");
                }

                shaderReloadPhase = 2;
                Console.WriteLine(
                    $"Shader reload: invalid candidate rejected; generation {rejected.Generation} retained.");
            }

            WindowSize size = window.ClientSize;
            float red = 0.08f + 0.04f * MathF.Sin(phase);
            float blue = 0.14f + 0.05f * MathF.Cos(phase);
            renderer.RenderFrame(
                size.Width,
                size.Height,
                window.IsMinimized,
                red,
                0.10f,
                blue);
            if (renderer.Generation != reportedGeneration)
            {
                reportedGeneration = renderer.Generation;
                VkExtent2D extent = renderer.Extent
                    ?? throw new InvalidOperationException(
                        "A live swapchain generation has no extent.");
                Console.WriteLine(
                    $"Swapchain generation {reportedGeneration}: "
                    + $"{extent.Width}x{extent.Height}.");
            }

            phase += 0.04f;
            Thread.Sleep(window.IsMinimized ? 50 : 16);
        }

        device.WaitIdle();
        VulkanValidationMessage[] validationErrors = instance.ValidationMessages
            .Where(message =>
                (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0)
            .ToArray();
        if (validationErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "Vulkan validation failed:" + Environment.NewLine
                + string.Join(
                    Environment.NewLine,
                    validationErrors.Select(message =>
                        $"[{message.IdName ?? message.Id.ToString(CultureInfo.InvariantCulture)}] {message.Message}")));
        }

        Console.WriteLine("Vulkan validation messenger reported no errors.");
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Vulkan probe failed: {exception.Message}");
    return 1;
}

static void PrintCapabilities(VulkanDeviceCapabilities capabilities)
{
    Console.WriteLine(
        "Device capabilities: "
        + $"dynamicRenderingLocalRead={capabilities.DynamicRenderingLocalRead}, "
        + $"descriptorBuffer={capabilities.DescriptorBuffer}, "
        + $"descriptorHeap={capabilities.DescriptorHeap}, "
        + $"bufferDeviceAddress={capabilities.BufferDeviceAddress}, "
        + $"bindingModel={capabilities.DescriptorBindingModel}, "
        + $"resourceHeapAlignment={capabilities.DescriptorHeapProperties?.ResourceHeapAlignment ?? 0}.");
}
