using ZEngine.UI;
using ZEngine.UI.Basic;

namespace ZEngine.Editor;

public interface IEditorPanel
{
    string Id { get; }

    string Title { get; }

    int Order { get; }

    void Compose(UiElement<EditorTheme> panel, EditorContext context);
}

public sealed class EditorPanelRegistry
{
    private readonly Dictionary<string, Registration> _panels = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public IDisposable Register(string ownerId, IEditorPanel panel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(panel);
        Registration registration = new(ownerId, panel);
        lock (_gate)
        {
            if (!_panels.TryAdd(panel.Id, registration))
            {
                Registration existing = _panels[panel.Id];
                throw new InvalidOperationException(
                    $"Editor panel '{panel.Id}' from '{ownerId}' conflicts with owner '{existing.OwnerId}'.");
            }
        }

        return new RemoveRegistration(this, registration);
    }

    public IReadOnlyList<IEditorPanel> Snapshot()
    {
        lock (_gate)
        {
            return _panels.Values
                .Select(registration => registration.Panel)
                .OrderBy(panel => panel.Order)
                .ThenBy(panel => panel.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private void Remove(Registration registration)
    {
        lock (_gate)
        {
            if (_panels.TryGetValue(registration.Panel.Id, out Registration? current)
                && ReferenceEquals(current, registration))
            {
                _panels.Remove(registration.Panel.Id);
            }
        }
    }

    private sealed record Registration(string OwnerId, IEditorPanel Panel);

    private sealed class RemoveRegistration(
        EditorPanelRegistry registry,
        Registration registration) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                registry.Remove(registration);
            }
        }
    }
}

public sealed class TextEditorPanel(
    string id,
    string title,
    int order,
    Func<EditorContext, IEnumerable<string>> lines) : IEditorPanel
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public int Order { get; } = order;

    public void Compose(UiElement<EditorTheme> panel, EditorContext context)
    {
        foreach (string line in lines(context).Take(12))
        {
            panel.TEXT(line);
        }
    }
}

public static class BuiltInEditorPanels
{
    public static IDisposable RegisterAll(EditorPanelRegistry registry)
    {
        List<IDisposable> registrations =
        [
            registry.Register("zengine.editor", Panel("scene", "场景层级", 10,
                context => context.Scene.Select(node =>
                    $"{new string(' ', node.Depth * 2)}{(node.Selected ? "●" : "○")} {node.Name}"))),
            registry.Register("zengine.editor", Panel("assets", "资源浏览器", 20,
                context => context.Assets.Select(asset =>
                    $"{asset.Kind}  {asset.Name}  g{asset.Generation}"))),
            registry.Register("zengine.editor", Panel("viewport", "Vulkan 视口", 30,
                context =>
                [
                    $"{context.Viewport.Width} × {context.Viewport.Height}",
                    $"Frame {context.Viewport.CompletedFrame}",
                    $"{context.Viewport.FrameMilliseconds:0.00} ms"
                ])),
            registry.Register("zengine.editor", Panel("inspector", "强类型检查器", 40,
                context => new[] { context.Inspector.Selection }.Concat(
                    context.Inspector.Properties.Select(property =>
                        $"{property.Name}: {property.Value} ({property.Type})")))),
            registry.Register("zengine.editor", Panel("console", "控制台", 50,
                context => context.Console.Snapshot(12).Select(entry =>
                    $"[{entry.Level}] {entry.Source}: {entry.Message}"))),
            registry.Register("zengine.editor", Panel("render-graph", "渲染图", 60,
                context => context.RenderPasses.Select(pass =>
                    $"{pass.Order}. {pass.Name} [{pass.Queue}]"))),
            registry.Register("zengine.editor", Panel("frame-profiler", "帧分析器", 70,
                context => [$"CPU frame {context.Viewport.FrameMilliseconds:0.00} ms"])),
            registry.Register("zengine.editor", Panel("ecs", "ECS Archetypes", 80,
                context => new[] { $"Entities {context.World.EntityCount}" }.Concat(
                    context.World.Archetypes.Select(archetype =>
                        $"0x{archetype.Mask:X}: {archetype.EntityCount} entities / {archetype.ChunkCount} chunks")))),
            registry.Register("zengine.editor", Panel("plugins", "插件依赖图", 90,
                context => context.Plugins.Select(plugin =>
                    $"{plugin.ActivationOrder}. {plugin.Id} {plugin.Version} ← {string.Join(", ", plugin.Dependencies)}"))),
            registry.Register("zengine.editor", Panel("reloads", "热重载历史", 100,
                context => context.Reloads.Entries.Select(entry =>
                    $"{entry.PluginId} g{entry.Generation}: {entry.Status}"))),
            registry.Register("zengine.editor", Panel("gpu", "GPU 资源", 110,
                context => context.GpuResources.Select(resource =>
                    $"{resource.Kind} {resource.Name}: {resource.Bytes / 1024d:0.0} KiB {resource.State}")))
        ];
        return new CompositeRegistration(registrations);
    }

    private static TextEditorPanel Panel(
        string id,
        string title,
        int order,
        Func<EditorContext, IEnumerable<string>> lines) =>
        new(id, title, order, lines);

    private sealed class CompositeRegistration(
        IReadOnlyList<IDisposable> registrations) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            for (int index = registrations.Count - 1; index >= 0; index--)
            {
                registrations[index].Dispose();
            }
        }
    }
}
