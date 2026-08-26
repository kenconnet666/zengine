using ZEngine.Plugins;
using ZEngine.RenderGraph;
using ZGame.Slice.Contracts;

namespace ZGame.Slice.RenderFeature;

public sealed class RenderFeaturePlugin : EnginePlugin
{
    public override void Configure(PluginScope scope)
    {
        _ = scope.Require<IGameplaySimulation>();
        scope.Export<IGameRenderFeature>(new GameRenderFeature());
    }
}

internal sealed class GameRenderFeature : IGameRenderFeature
{
    private readonly CompiledRenderGraph _graph;

    public GameRenderFeature()
    {
        RenderGraphBuilder graph = new();
        AddPass(graph, "Game.Clear");
        AddPass(graph, "Game.World", "Game.Clear");
        AddPass(graph, "Game.Effects", "Game.World");
        AddPass(graph, "Game.Hud", "Game.Effects");
        AddPass(graph, "Game.Present", "Game.Hud");
        _graph = graph.Compile();
        PassNames = _graph.Passes.Select(pass => pass.Name).ToArray();
    }

    public IReadOnlyList<string> PassNames { get; }

    public int Execute(in GameplaySnapshot gameplay)
    {
        _ = gameplay;
        _graph.Execute(NullRenderGraphCommandSink.Instance);
        return _graph.Passes.Count;
    }

    public void Dispose()
    {
    }

    private static void AddPass(
        RenderGraphBuilder graph,
        string name,
        params string[] dependencies) =>
        graph.Pass<PassData>(name, pass =>
        {
            if (dependencies.Length > 0)
            {
                pass.After(dependencies);
            }

            pass.SideEffect();
            pass.Execute(static (
                ref RenderGraphCommandContext _,
                in PassData _) =>
            {
            });
        });

    private readonly struct PassData;
}
