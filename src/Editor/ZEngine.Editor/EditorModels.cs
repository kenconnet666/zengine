using ZEngine.Assets;
using ZEngine.Ecs;
using ZEngine.Plugins;
using ZEngine.Plugins.Runtime;

namespace ZEngine.Editor;

public sealed record SceneNodeView(
    string Name,
    int Depth,
    bool Selected = false);

public sealed record InspectorPropertyView(
    string Name,
    string Type,
    string Value,
    bool Writable);

public sealed record InspectorView(
    string Selection,
    IReadOnlyList<InspectorPropertyView> Properties);

public sealed record EditorAssetView(
    string Name,
    string Kind,
    ContentHash Hash,
    ulong Generation);

public sealed record ViewportView(
    int Width,
    int Height,
    long CompletedFrame,
    double FrameMilliseconds);

public sealed record RenderPassView(
    string Name,
    int Order,
    string Queue);

public sealed record GpuResourceView(
    string Name,
    string Kind,
    long Bytes,
    string State);

public sealed record PluginNodeView(
    string Id,
    string Version,
    int ActivationOrder,
    IReadOnlyList<string> Dependencies);

public sealed record ReloadHistoryEntry(
    DateTimeOffset Timestamp,
    string PluginId,
    PluginReloadStatus Status,
    ulong Generation,
    string? Error);

public sealed class ReloadHistory(int capacity = 128)
{
    private readonly Queue<ReloadHistoryEntry> _entries = new(capacity);

    public IReadOnlyList<ReloadHistoryEntry> Entries => _entries.ToArray();

    public void Record(
        string pluginId,
        PluginReloadResult result)
    {
        while (_entries.Count >= capacity)
        {
            _entries.Dequeue();
        }

        _entries.Enqueue(new(
            DateTimeOffset.UtcNow,
            pluginId,
            result.Status,
            result.ActiveGeneration.Value,
            result.Error));
    }
}

public static class PluginGraphInspector
{
    public static IReadOnlyList<PluginNodeView> Inspect(
        IEnumerable<PluginManifest> manifests)
    {
        PluginGraphPlan graph = PluginGraph.Resolve(manifests);
        return graph.ActivationOrder
            .Select((manifest, order) => new PluginNodeView(
                manifest.Id,
                manifest.Version,
                order,
                manifest.Dependencies
                    .Where(dependency => dependency.Kind == PluginDependencyKind.Required)
                    .Select(dependency => dependency.Id)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }
}

public sealed class EditorContext(EditorConsole console)
{
    public EditorConsole Console { get; } = console;

    public IReadOnlyList<SceneNodeView> Scene { get; set; } = [];

    public InspectorView Inspector { get; set; } = new("Nothing selected", []);

    public IReadOnlyList<EditorAssetView> Assets { get; set; } = [];

    public ViewportView Viewport { get; set; } = new(0, 0, 0, 0);

    public IReadOnlyList<RenderPassView> RenderPasses { get; set; } = [];

    public WorldSnapshot World { get; set; } = new(0, []);

    public IReadOnlyList<PluginNodeView> Plugins { get; set; } = [];

    public ReloadHistory Reloads { get; } = new();

    public IReadOnlyList<GpuResourceView> GpuResources { get; set; } = [];
}
