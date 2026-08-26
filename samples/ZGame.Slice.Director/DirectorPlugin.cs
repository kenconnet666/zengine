using ZEngine.Plugins;
using ZGame.Slice.Contracts;

namespace ZGame.Slice.Director;

public sealed class DirectorPlugin : EnginePlugin
{
    public override void Configure(PluginScope scope) =>
        scope.Export<IGameDirector>(new GameDirector(
            scope.Require<IGameplaySimulation>(),
            scope.Require<IGameRenderFeature>()));
}

internal sealed class GameDirector(
    IGameplaySimulation gameplay,
    IGameRenderFeature render) : IGameDirector
{
    private long _presentedFrame;

    public GameFrame Frame { get; private set; } = new(
        gameplay.Snapshot,
        render.PassNames.Count,
        0);

    public GameFrame Tick(float deltaSeconds)
    {
        GameplaySnapshot snapshot = gameplay.Step(deltaSeconds);
        int passes = render.Execute(snapshot);
        return Frame = new(snapshot, passes, ++_presentedFrame);
    }

    public void Dispose()
    {
    }
}
