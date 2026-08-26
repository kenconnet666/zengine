using ZEngine.Ecs;
using ZEngine.PlayProbe;
using ZEngine.Plugins;
using ZEngine.UI;
using Xunit;

namespace ZEngine.Editor.Tests;

public sealed class EditorTests
{
    [Fact]
    public void EditorUsesEngineUiAndExposesAllInitialPanels()
    {
        World world = new();
        world.Spawn(new Position(1, 2));
        world.Spawn(new Position(3, 4));
        EditorConsole console = new();
        console.Write(EditorLogLevel.Information, "Test", "Editor ready");
        EditorContext context = new(console)
        {
            Scene = [new("World", 0), new("Player", 1, true)],
            Inspector = new(
                "Player",
                [new("Position", "Vector2", "1, 2", true)]),
            Viewport = new(1280, 720, 42, 6.5),
            World = world.Inspect(),
            RenderPasses = [new("Frame.Ui", 3, "Graphics")],
            Plugins = PluginGraphInspector.Inspect(Manifests()),
            GpuResources = [new("ui-atlas", "Image", 262_144, "Resident")]
        };
        EditorPanelRegistry panels = new();
        using IDisposable builtIns = BuiltInEditorPanels.RegisterAll(panels);
        Assert.Equal(11, panels.Snapshot().Count);

        using EditorShell shell = new(panels, context);
        UiTree tree = shell.Render(new());
        string[] semanticLabels = Flatten(tree.Root)
            .Select(node => node.SemanticLabel)
            .Where(label => label is not null)
            .Cast<string>()
            .ToArray();
        string[] text = Flatten(tree.Root)
            .Select(node => node.Text)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains("ZEngine 编辑器", semanticLabels);
        Assert.Contains("Vulkan 视口", semanticLabels);
        Assert.Contains("强类型检查器", semanticLabels);
        Assert.Contains("控制台", semanticLabels);
        Assert.Contains("插件依赖图", semanticLabels);
        Assert.Contains("Player", text);
        Assert.Contains(text, value => value.Contains("gameplay", StringComparison.Ordinal));
        Assert.Equal(2, context.World.EntityCount);
        Assert.Single(context.World.Archetypes);
    }

    [Fact]
    public void PanelOwnershipRejectsConflictsAndUnregistersExactly()
    {
        EditorPanelRegistry panels = new();
        TextEditorPanel panel = new("plugin-panel", "Plugin", 1, _ => []);
        using IDisposable registration = panels.Register("plugin.a", panel);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            panels.Register("plugin.b", new TextEditorPanel(
                "plugin-panel",
                "Other",
                2,
                _ => [])));
        Assert.Contains("plugin.a", error.Message);
        registration.Dispose();
        using IDisposable replacement = panels.Register(
            "plugin.b",
            new TextEditorPanel("plugin-panel", "Other", 2, _ => []));
        Assert.Single(panels.Snapshot());
    }

    [Fact]
    public async Task CrashedGameProcessDoesNotTakeEditorDown()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        EditorConsole console = new();
        PlayProcessSupervisor supervisor = new(console);
        string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? "dotnet";
        await using PlayProcessSession session = supervisor.Start(
            host,
            [typeof(PlayProbeMarker).Assembly.Location, "--crash"]);
        PlayProcessResult result = await session.WaitForExitAsync(cancellation);

        Assert.True(result.Crashed);
        Assert.Equal(23, result.ExitCode);
        Assert.Contains("ZEngine play process initialized.", result.StandardOutput);
        Assert.Contains("Intentional isolated game crash.", result.StandardError);
        Assert.Contains(
            console.Snapshot(),
            entry => entry.Level == EditorLogLevel.Error
                     && entry.Message.Contains("exit code 23", StringComparison.Ordinal));

        EditorPanelRegistry stillAlive = new();
        using IDisposable registration = BuiltInEditorPanels.RegisterAll(stillAlive);
        Assert.Equal(11, stillAlive.Snapshot().Count);
    }

    private static IEnumerable<UiNode> Flatten(UiNode node)
    {
        yield return node;
        foreach (UiNode child in node.Children)
        {
            foreach (UiNode descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static PluginManifest[] Manifests() =>
    [
        Manifest("core"),
        Manifest("gameplay", new PluginDependency("core", "[1.0.0,2.0.0)")),
        Manifest("hud", new PluginDependency("gameplay", "1.0.0"))
    ];

    private static PluginManifest Manifest(
        string id,
        params PluginDependency[] dependencies) =>
        new(
            id,
            "1.0.0",
            $"{id}.dll",
            $"{id}.Plugin",
            [],
            dependencies,
            []);

    private readonly record struct Position(float X, float Y);
}
