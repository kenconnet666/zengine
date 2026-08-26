using System.Numerics;
using Xunit;

namespace ZEngine.Ecs.Tests;

public sealed class RuntimeBoundaryTests
{
    [Fact]
    public void FixedClockClampsCatchupAndReportsDroppedTime()
    {
        EngineLoopClock clock = new(
            new(
                TimeSpan.FromMilliseconds(10),
                TimeSpan.FromMilliseconds(100),
                4));

        FrameSchedule first = clock.Advance(TimeSpan.FromMilliseconds(6));
        Assert.Equal(0, first.FixedStepCount);
        Assert.Equal(0.6, first.InterpolationAlpha, precision: 6);

        FrameSchedule second = clock.Advance(TimeSpan.FromMilliseconds(9));
        Assert.Equal(1, second.FixedStepCount);
        Assert.Equal(0.5, second.InterpolationAlpha, precision: 6);

        FrameSchedule clamped = clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(4, clamped.FixedStepCount);
        Assert.True(clamped.DroppedAccumulatedTime);
        Assert.InRange(clamped.InterpolationAlpha, 0, 1);
    }

    [Fact]
    public void DoubleBufferedRenderWorldIsDetachedAndSteadyExtractionAllocatesNothing()
    {
        World world = new();
        Entity first = Entity.Null;
        for (int index = 0; index < 4_096; index++)
        {
            Entity entity = world.Spawn(
                new Transform(new(index, 0, 0)),
                new RenderItem(index));
            if (index == 0)
            {
                first = entity;
            }
        }

        RenderWorldBuffer<Transform, RenderItem> buffer = new();
        RenderWorldSnapshot<Transform, RenderItem> initial = buffer.Extract(world);
        world.Get<Transform>(first).Position = new(999, 0, 0);

        Assert.Equal(0, initial.First[0].Position.X);
        RenderWorldSnapshot<Transform, RenderItem> updated = buffer.Extract(world);
        Assert.Equal(999, updated.First[0].Position.X);
        Assert.NotEqual(initial.Generation, updated.Generation);

        buffer.Extract(world);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 100; iteration++)
        {
            buffer.Extract(world);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void SceneRoundTripUsesStableSchemasAndUnmanagedPayloads()
    {
        World source = new();
        source.Spawn(
            new Transform(new(1, 2, 3)),
            new RenderItem(17));
        source.Spawn(
            new Transform(new(4, 5, 6)),
            new RenderItem(29));
        using MemoryStream stream = new();

        SceneSerializer.Save(source, stream);
        stream.Position = 0;
        World loaded = SceneSerializer.Load(stream);

        Assert.Equal(2, loaded.EntityCount);
        int itemSum = 0;
        Vector3 positionSum = Vector3.Zero;
        Query<Read<Transform>, Read<RenderItem>> query =
            loaded.Query<Read<Transform>, Read<RenderItem>>();
        foreach (QueryChunk chunk in query.Chunks)
        {
            ReadOnlySpan<Transform> transforms = chunk.Read<Transform>();
            ReadOnlySpan<RenderItem> items = chunk.Read<RenderItem>();
            for (int index = 0; index < chunk.Count; index++)
            {
                positionSum += transforms[index].Position;
                itemSum += items[index].MeshId;
            }
        }

        Assert.Equal(new Vector3(5, 7, 9), positionSum);
        Assert.Equal(46, itemSum);
    }

    [Component]
    private struct Transform(Vector3 position)
    {
        public Vector3 Position = position;
    }

    [Component]
    private readonly struct RenderItem(int meshId)
    {
        public readonly int MeshId = meshId;
    }
}
