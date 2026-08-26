using ZEngine.Ecs;
using ZEngine.Plugins;
using ZGame.Slice.Contracts;

namespace ZGame.Slice.Gameplay;

public sealed class GameplayPlugin : EnginePlugin
{
    public override void Configure(PluginScope scope) =>
        scope.Export<IGameplaySimulation>(new GameplaySimulation(50_000));
}

internal sealed class GameplaySimulation : IGameplaySimulation
{
    private readonly World _world = new();
    private long _tick;
    private float _playerHealth = 100;
    private float _playerX;
    private float _playerY;

    public GameplaySimulation(int entityCount)
    {
        for (int index = 0; index < entityCount; index++)
        {
            float x = index % 500;
            float y = index / 500;
            float velocityX = (index % 7 - 3) * 0.1f;
            float velocityY = (index % 5 - 2) * 0.08f;
            _world.Spawn(
                new SimPosition(x, y),
                new SimVelocity(velocityX, velocityY));
        }
    }

    public GameplaySnapshot Snapshot => new(
        _tick,
        _world.EntityCount,
        _playerHealth,
        _playerX,
        _playerY);

    public GameplaySnapshot Step(float deltaSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deltaSeconds);
        Query<Write<SimPosition>, Read<SimVelocity>> query =
            _world.Query<Write<SimPosition>, Read<SimVelocity>>();
        foreach (QueryChunk chunk in query.Chunks)
        {
            Span<SimPosition> positions = chunk.Write<SimPosition>();
            ReadOnlySpan<SimVelocity> velocities = chunk.Read<SimVelocity>();
            for (int index = 0; index < chunk.Count; index++)
            {
                positions[index].X += velocities[index].X * deltaSeconds;
                positions[index].Y += velocities[index].Y * deltaSeconds;
            }
        }

        _tick++;
        _playerX += 2.5f * deltaSeconds;
        _playerY = MathF.Sin(_playerX * 0.1f) * 3;
        _playerHealth = MathF.Max(0, _playerHealth - deltaSeconds * 0.01f);
        return Snapshot;
    }

    public void Dispose()
    {
    }

    private struct SimPosition(float x, float y)
    {
        public float X = x;
        public float Y = y;
    }

    private readonly struct SimVelocity(float x, float y)
    {
        public readonly float X = x;
        public readonly float Y = y;
    }
}
