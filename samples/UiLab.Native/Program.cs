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

float? scaleOverride = null;
string? scaleArgument = args.FirstOrDefault(argument =>
    argument.StartsWith("--ui-scale=", StringComparison.Ordinal));
if (scaleArgument is not null)
{
    scaleOverride = float.Parse(
        scaleArgument["--ui-scale=".Length..],
        System.Globalization.CultureInfo.InvariantCulture);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaleOverride.Value);
}

using LoginCard component = new();
AppTheme theme = new();
using Win32Window window = Win32Window.Create(
    "ZEngine Native UiLab",
    960,
    640,
    visible: false);
if (scaleOverride is { } syntheticScale)
{
    window.ResizeClient(
        checked((uint)MathF.Round(960 * syntheticScale)),
        checked((uint)MathF.Round(640 * syntheticScale)));
}

window.Show();
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
VulkanUiScene scene = new();
using GdiGlyphRasterizer glyphRasterizer = new();
UiScaleContext currentScale = default;
UiLayout? currentLayout = null;
int paintCount = 0;
UpdateUi(force: true);
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
    overlayFactory: overlayFactory,
    captureFactory: new VulkanFrameCaptureFactory());
UiRenderTelemetry initialTelemetry = scene.Telemetry;
Console.WriteLine(
    $"Native UiLab: logical={currentScale.LogicalWidth:0.#}x{currentScale.LogicalHeight:0.#}, physical={currentScale.PhysicalWidth}x{currentScale.PhysicalHeight}, dpi={currentScale.DpiX}, scale={currentScale.ScaleX:0.##}, nodes={currentLayout!.Bounds.Count}, paint={paintCount}, instances={initialTelemetry.InstanceCount}, draws={initialTelemetry.DrawCount}, glyphHits={initialTelemetry.GlyphCacheHits}, glyphMisses={initialTelemetry.GlyphCacheMisses}, compileMs={initialTelemetry.SceneCompileMilliseconds:0.###}.");

DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
while (DateTime.UtcNow < deadline && window.PumpEvents())
{
    UpdateUi(force: false);
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

byte[] capturePng = renderer.CapturePng();
Console.WriteLine($"Native UiLab capture: {capturePng.Length} PNG bytes.");
string? captureArgument = args.FirstOrDefault(argument =>
    argument.StartsWith("--capture=", StringComparison.Ordinal));
if (captureArgument is not null)
{
    string capturePath = Path.GetFullPath(
        captureArgument["--capture=".Length..]);
    File.WriteAllBytes(capturePath, capturePng);
    Console.WriteLine($"Native UiLab capture written to {capturePath}.");
}

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

void UpdateUi(bool force)
{
    WindowSize physical = window.ClientSize;
    if (physical.Width == 0 || physical.Height == 0)
    {
        return;
    }

    WindowDpi dpi = window.Dpi;
    UiScaleContext next = scaleOverride is { } requestedScale
        ? UiScaleContext.FromScale(
            physical.Width,
            physical.Height,
            requestedScale)
        : UiScaleContext.FromDpi(
            physical.Width,
            physical.Height,
            dpi.X,
            dpi.Y);
    if (!force && next == currentScale)
    {
        return;
    }

    UiTree tree = component.Render(theme);
    UiLayout layout = new UiLayoutEngine().Layout(
        tree,
        next.LogicalWidth,
        next.LogicalHeight);
    IReadOnlyList<UiPaintCommand> paint =
        new UiPaintCompiler().Compile(tree, layout, next);
    scene.Update(paint, glyphRasterizer);
    currentScale = next;
    currentLayout = layout;
    paintCount = paint.Count;
}

static byte[] ReadShader(string name) =>
    File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Shaders", name));
