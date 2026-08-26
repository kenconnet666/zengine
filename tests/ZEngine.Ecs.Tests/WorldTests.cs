using System.Diagnostics;
using System.Numerics;
using Xunit;

namespace ZEngine.Ecs.Tests;

public sealed class WorldTests
{
    [Fact]
    public void RejectsStaleEntityAfterIndexReuse()
    {
        World world = new();
        Entity first = world.Spawn(new Position(new(1, 2, 3)));
        world.Destroy(first);
        Entity second = world.Spawn(new Position(new(4, 5, 6)));

        Assert.Equal(first.Index, second.Index);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.False(world.IsAlive(first));
        Assert.True(world.IsAlive(second));
        Assert.Throws<InvalidOperationException>(() => world.Get<Position>(first));
    }

    [Fact]
    public void MovesBetweenArchetypesAtCommandBarrierAndPreservesColumns()
    {
        World world = new();
        Entity entity = world.Spawn(
            new Position(new(3, 4, 5)),
            new Velocity(new(1, 0, -1)));
        EntityCommandBuffer commands = new();
        commands.Add(entity, new Health(100));

        Assert.False(world.Has<Health>(entity));
        Assert.Equal(1, commands.Flush(world));
        Assert.True(world.Has<Health>(entity));
        Assert.Equal(new Vector3(3, 4, 5), world.Get<Position>(entity).Value);
        Assert.Equal(new Vector3(1, 0, -1), world.Get<Velocity>(entity).Value);
        Assert.Equal(100, world.Get<Health>(entity).Value);

        commands.Remove<Velocity>(entity);
        Assert.Equal(1, commands.Flush(world));
        Assert.False(world.Has<Velocity>(entity));
        Assert.Equal(new Vector3(3, 4, 5), world.Get<Position>(entity).Value);
        Assert.Equal(100, world.Get<Health>(entity).Value);
    }

    [Fact]
    public void TypedChunkQueryUpdatesWithoutSteadyManagedAllocation()
    {
        World world = new();
        Entity first = Entity.Null;
        for (int index = 0; index < 10_000; index++)
        {
            Entity entity = world.Spawn(
                new Position(new(index, 0, 0)),
                new Velocity(new(0.5f, 1, -0.25f)));
            if (index == 0)
            {
                first = entity;
            }
        }

        Update(world, 0.016f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 100; iteration++)
        {
            Update(world, 0.016f);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(0.808f, world.Get<Position>(first).Value.X, precision: 5);
        Assert.Equal(1.616f, world.Get<Position>(first).Value.Y, precision: 5);
    }

    [Fact]
    public void UpdatesOneMillionEntitiesThroughChunkSoa()
    {
        World world = new();
        Stopwatch creation = Stopwatch.StartNew();
        for (int index = 0; index < 1_000_000; index++)
        {
            world.Spawn(
                new Position(new(index, index, index)),
                new Velocity(Vector3.One));
        }

        creation.Stop();
        Stopwatch update = Stopwatch.StartNew();
        Update(world, 0.5f);
        update.Stop();
        Console.WriteLine(
            $"ECS million benchmark: create={creation.Elapsed.TotalMilliseconds:F2} ms, "
            + $"update={update.Elapsed.TotalMilliseconds:F2} ms");

        Assert.Equal(1_000_000, world.EntityCount);
        Assert.True(
            creation.Elapsed < TimeSpan.FromSeconds(15),
            $"Million-entity creation took {creation.Elapsed}.");
        Assert.True(
            update.Elapsed < TimeSpan.FromSeconds(2),
            $"Million-entity update took {update.Elapsed}.");
    }

    private static void Update(World world, float deltaTime)
    {
        Query<Write<Position>, Read<Velocity>> query =
            world.Query<Write<Position>, Read<Velocity>>();
        foreach (QueryChunk chunk in query.Chunks)
        {
            Span<Position> positions = chunk.Write<Position>();
            ReadOnlySpan<Velocity> velocities = chunk.Read<Velocity>();
            for (int index = 0; index < chunk.Count; index++)
            {
                positions[index].Value += velocities[index].Value * deltaTime;
            }
        }
    }

    [Component]
    private struct Position(Vector3 value)
    {
        public Vector3 Value = value;
    }

    [Component]
    private readonly struct Velocity(Vector3 value)
    {
        public readonly Vector3 Value = value;
    }

    [Component]
    private readonly struct Health(int value)
    {
        public readonly int Value = value;
    }
}
