namespace ZGame.Slice.Contracts;

public readonly record struct GameplaySnapshot(
    long Tick,
    int EntityCount,
    float PlayerHealth,
    float PlayerX,
    float PlayerY);

public readonly record struct GameFrame(
    GameplaySnapshot Gameplay,
    int RenderPassCount,
    long PresentedFrame);

public interface IGameplaySimulation : IDisposable
{
    GameplaySnapshot Snapshot { get; }

    GameplaySnapshot Step(float deltaSeconds);
}

public interface IGameRenderFeature : IDisposable
{
    IReadOnlyList<string> PassNames { get; }

    int Execute(in GameplaySnapshot gameplay);
}

public interface IGameDirector : IDisposable
{
    GameFrame Frame { get; }

    GameFrame Tick(float deltaSeconds);
}
