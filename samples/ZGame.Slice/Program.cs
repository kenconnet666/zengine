using System.Diagnostics;
using ZEngine.Graphics.Vulkan;
using ZEngine.Platform;
using ZEngine.Platform.Win32;
using ZEngine.UI;
using ZEngine.UI.Rendering;
using ZEngine.UI.Vulkan;
using ZEngine.UI.Windows;
using ZEngine.Vulkan.Raw;
using ZGame.Slice;
using ZGame.Slice.Contracts;
using ZGame.Slice.UI;

CancellationToken cancellation = CancellationToken.None;
string? cacheArgument = FindArgument("--cache=");
await using GameSliceRuntime runtime = await GameSliceRuntime.CreateAsync(
    cacheArgument,
    cancellationToken: cancellation);
for (int index = 0; index < 32; index++)
{
    runtime.Tick(1f / 60);
}

if (args.Contains("--reload", StringComparer.Ordinal))
{
    var reload = await runtime.ReloadGameplayAsync(cancellation);
    Console.WriteLine(
        $"Gameplay reload: {reload.Status}; closure={string.Join(',', reload.ReloadedPlugins)}");
}

int requestedFrames = int.TryParse(FindArgument("--frames="), out int parsedFrames)
    ? parsedFrames
    : 600;
if (args.Contains("--headless", StringComparer.Ordinal))
{
    Stopwatch timer = Stopwatch.StartNew();
    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    GameFrame frame = default;
    for (int index = 0; index < requestedFrames; index++)
    {
        frame = runtime.Tick(1f / 60);
    }

    long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    timer.Stop();
    double perFrame = timer.Elapsed.TotalMilliseconds / requestedFrames;
    Console.WriteLine(
        $"Headless slice: frames={requestedFrames}, entities={frame.Gameplay.EntityCount}, passes={frame.RenderPassCount}, totalMs={timer.Elapsed.TotalMilliseconds:0.000}, msPerFrame={perFrame:0.0000}, allocatedBytes={allocated}.");
    Console.WriteLine(
        $"Plugins={string.Join(',', runtime.PluginIds)}; residentAssets={runtime.AssetResidency.Count}.");
    return 0;
}

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The current native game slice validates Win32 Vulkan. Use --headless elsewhere.");
    return 2;
}

double seconds = double.TryParse(
    FindArgument("--seconds="),
    System.Globalization.NumberStyles.Float,
    System.Globalization.CultureInfo.InvariantCulture,
    out double parsedSeconds)
    ? parsedSeconds
    : 3;
using GameHud hud = new();
GameTheme theme = new();
hud.Update(runtime.Frame);
using Win32Window window = Win32Window.Create(
    "ZEngine First Game Slice",
    1120,
    700,
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
VulkanUiScene uiScene = new();
using GdiGlyphRasterizer glyphRasterizer = new();
UpdateUi();
VulkanUiOverlayFactory overlayFactory = new(
    uiScene,
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

Stopwatch frameTimer = Stopwatch.StartNew();
double tickMilliseconds = 0;
long frameCount = 0;
DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
while (DateTime.UtcNow < deadline && window.PumpEvents())
{
    long start = Stopwatch.GetTimestamp();
    GameFrame frame = runtime.Tick(1f / 60);
    tickMilliseconds += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    if (frameCount % 15 == 0)
    {
        hud.Update(frame);
        UpdateUi();
    }

    WindowSize current = window.ClientSize;
    renderer.RenderFrame(
        current.Width,
        current.Height,
        window.IsMinimized,
        0.015f,
        0.025f,
        0.055f);
    frameCount++;
}

frameTimer.Stop();
byte[] capture = renderer.CapturePng();
string? capturePathArgument = FindArgument("--capture=");
if (capturePathArgument is not null)
{
    string capturePath = Path.GetFullPath(capturePathArgument);
    Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
    File.WriteAllBytes(capturePath, capture);
    Console.WriteLine($"Game slice capture written to {capturePath} ({capture.Length} bytes).");
}

VulkanValidationMessage[] validationErrors = instance.ValidationMessages
    .Where(message =>
        (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0)
    .ToArray();
Console.WriteLine(
    $"Native slice: frames={frameCount}, fps={frameCount / frameTimer.Elapsed.TotalSeconds:0.0}, gameplayMs={tickMilliseconds / Math.Max(1, frameCount):0.000}, assets={runtime.AssetResidency.Count}, validationErrors={validationErrors.Length}.");
if (validationErrors.Length > 0)
{
    Console.Error.WriteLine(string.Join(
        Environment.NewLine,
        validationErrors.Select(error => error.Message)));
    return 1;
}

return 0;

void UpdateUi()
{
    WindowSize current = window.ClientSize;
    if (current.Width == 0 || current.Height == 0)
    {
        return;
    }

    UiTree tree = hud.Render(theme);
    UiLayout layout = new UiLayoutEngine().Layout(
        tree,
        current.Width,
        current.Height);
    uiScene.Update(new UiPaintCompiler().Compile(tree, layout), glyphRasterizer);
}

string? FindArgument(string prefix) => args
    .FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal))?
    [prefix.Length..];

static byte[] ReadShader(string name) =>
    File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", name));
