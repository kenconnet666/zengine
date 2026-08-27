using ZEngine.Assets;
using ZEngine.Editor;
using ZEngine.Graphics.Vulkan;
using ZEngine.Platform;
using ZEngine.Platform.Win32;
using ZEngine.Plugins;
using ZEngine.UI;
using ZEngine.UI.Rendering;
using ZEngine.UI.Vulkan;
using ZEngine.UI.Windows;
using ZEngine.Vulkan.Raw;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("EditorLab.Native currently validates the Win32 Vulkan adapter.");
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

EditorConsole console = new();
console.Write(EditorLogLevel.Information, "Assets", "3 content revisions are resident");
console.Write(EditorLogLevel.Warning, "Play", "Crash isolation is active");
EditorContext context = new(console)
{
    Scene =
    [
        new("Level", 0),
        new("Camera", 1),
        new("Player", 1, true),
        new("Directional Light", 1)
    ],
    Inspector = new(
        "Player",
        [
            new("Position", "Vector3", "0, 1, 0", true),
            new("Health", "float", "100", true),
            new("Mesh", "AssetId<MeshAsset>", "Hero", true)
        ]),
    Assets =
    [
        new("level.scene", "Scene", new(new string('1', 64)), 4),
        new("surface.spv", "Shader", new(new string('2', 64)), 12),
        new("albedo.png", "Texture", new(new string('3', 64)), 7)
    ],
    Viewport = new(1280, 720, 1842, 5.72),
    RenderPasses =
    [
        new("Frame.Clear", 0, "Graphics"),
        new("Frame.World", 1, "Graphics"),
        new("Frame.Ui", 2, "Graphics"),
        new("Frame.Present", 3, "Present")
    ],
    Plugins = PluginGraphInspector.Inspect(
    [
        Manifest("game.contracts"),
        Manifest("game.gameplay", new PluginDependency("game.contracts", "1.0.0")),
        Manifest("game.render", new PluginDependency("game.gameplay", "1.0.0"))
    ]),
    GpuResources =
    [
        new("ui-glyph-atlas", "Image", 4 * 1024 * 1024, "Resident"),
        new("frame-ring", "Buffer", 768 * 1024, "Timeline 1842")
    ]
};
EditorPanelRegistry panels = new();
using IDisposable builtIns = BuiltInEditorPanels.RegisterAll(panels);
using EditorShell component = new(panels, context);
EditorTheme theme = new();
using Win32Window window = Win32Window.Create(
    "ZEngine Editor Lab",
    1280,
    760,
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
console.Write(
    EditorLogLevel.Information,
    "Vulkan",
    $"{physicalDevice.Name} · Vulkan {physicalDevice.ApiVersion} · "
    + device.Capabilities.DescriptorBindingModel);
WindowSize size = window.ClientSize;
UiTree tree = component.Render(theme);
UiLayout layout = new UiLayoutEngine().Layout(tree, size.Width, size.Height);
IReadOnlyList<UiPaintCommand> paint = new UiPaintCompiler().Compile(tree, layout);
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
    overlayFactory: overlayFactory,
    captureFactory: new VulkanFrameCaptureFactory());
Console.WriteLine(
    $"Editor Lab: panels={panels.Snapshot().Count}, nodes={layout.Bounds.Count}, paint={paint.Count}, quads={scene.Quads.Count}.");

DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
while (DateTime.UtcNow < deadline && window.PumpEvents())
{
    WindowSize current = window.ClientSize;
    renderer.RenderFrame(
        current.Width,
        current.Height,
        window.IsMinimized,
        0.018f,
        0.022f,
        0.032f);
    Thread.Sleep(16);
}

byte[] capturePng = renderer.CapturePng();
string? captureArgument = args.FirstOrDefault(argument =>
    argument.StartsWith("--capture=", StringComparison.Ordinal));
if (captureArgument is not null)
{
    string capturePath = Path.GetFullPath(captureArgument["--capture=".Length..]);
    Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
    File.WriteAllBytes(capturePath, capturePng);
    Console.WriteLine($"Editor Lab capture written to {capturePath} ({capturePng.Length} bytes).");
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

Console.WriteLine("Editor Lab Vulkan validation reported no errors.");
return 0;

static PluginManifest Manifest(
    string id,
    params PluginDependency[] dependencies) =>
    new(id, "1.0.0", $"{id}.dll", $"{id}.Plugin", [], dependencies, []);

static byte[] ReadShader(string name) =>
    File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", name));
