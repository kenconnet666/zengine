using UiLab.Shared;
using ZEngine.Graphics.Vulkan;
using ZEngine.Platform;
using ZEngine.Platform.Win32;
using ZEngine.UI;
using ZEngine.UI.Rendering;
using ZEngine.UI.Vulkan;
using ZEngine.UI.Windows;
using ZEngine.Vulkan.Raw;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("UiLab.Native currently validates the Win32 Vulkan adapter.");
    return 2;
}

double seconds = 3;
string? duration = args.FirstOrDefault(argument =>
    argument.StartsWith("--seconds=", StringComparison.Ordinal));
if (duration is not null)
{
    seconds = double.Parse(
        duration["--seconds=".Length..],
        System.Globalization.CultureInfo.InvariantCulture);
}

using LoginCard component = new();
AppTheme theme = new();
using Win32Window window = Win32Window.Create(
    "ZEngine Native UiLab",
    960,
    640,
    visible: true);
using VulkanLoader loader = VulkanLoader.OpenSystem();
using VulkanInstance instance = VulkanInstance.Create(
    loader,
    VulkanInstanceOptions.Win32Surface);
VulkanPhysicalDeviceInfo physicalDevice = instance.EnumeratePhysicalDevices()
    .OrderByDescending(device => device.DeviceType == VkPhysicalDeviceType.DiscreteGpu)
    .First();
using VulkanSurface surface = instance.CreateWin32Surface(
    window.NativeInstance,
    window.NativeHandle);
using VulkanDevice device = instance.CreateGraphicsDevice(
    physicalDevice,
    enableSwapchain: true);
WindowSize size = window.ClientSize;
UiTree tree = component.Render(theme);
UiLayout layout = new UiLayoutEngine().Layout(
    tree,
    size.Width,
    size.Height);
IReadOnlyList<UiPaintCommand> paint =
    new UiPaintCompiler().Compile(tree, layout);
VulkanUiScene scene = new();
using GdiGlyphRasterizer glyphRasterizer = new();
scene.Update(paint, glyphRasterizer);
VulkanUiOverlayFactory overlayFactory = new(
    scene,
    ReadShader("ui.vert.spv"),
    ReadShader("ui.frag.spv"));
using VulkanWindowRenderer renderer = new(
    device,
    physicalDevice,
    surface,
    ReadShader("triangle.vert.spv"),
    ReadShader("triangle.frag.spv"),
    overlayFactory: overlayFactory);
Console.WriteLine(
    $"Native UiLab: nodes={layout.Bounds.Count}, paint={paint.Count}, quads={scene.Quads.Count}.");

DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
while (DateTime.UtcNow < deadline && window.PumpEvents())
{
    WindowSize current = window.ClientSize;
    renderer.RenderFrame(
        current.Width,
        current.Height,
        window.IsMinimized,
        0.025f,
        0.03f,
        0.045f);
    Thread.Sleep(16);
}

device.WaitIdle();
VulkanValidationMessage[] errors = instance.ValidationMessages
    .Where(message =>
        (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0)
    .ToArray();
if (errors.Length > 0)
{
    Console.Error.WriteLine(string.Join(
        Environment.NewLine,
        errors.Select(error => error.Message)));
    return 1;
}

Console.WriteLine("Native UiLab validation reported no errors.");
return 0;

static byte[] ReadShader(string name) =>
    File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Shaders", name));
