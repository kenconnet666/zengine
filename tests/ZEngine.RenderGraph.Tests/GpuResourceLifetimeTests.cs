using ZEngine.Graphics;
using Xunit;

namespace ZEngine.RenderGraph.Tests;

public sealed class GpuResourceLifetimeTests
{
    [Fact]
    public void RecycledSlotRejectsStaleGeneration()
    {
        GpuResourcePool<TextureTag, TestResource> pool = new();
        TestResource first = new("first");
        GpuHandle<TextureTag> oldHandle = pool.Add(first);

        Assert.Same(first, pool.Get(oldHandle));
        Assert.Same(first, pool.Remove(oldHandle));
        Assert.False(pool.TryGet(oldHandle, out _));

        TestResource second = new("second");
        GpuHandle<TextureTag> newHandle = pool.Add(second);

        Assert.Equal(oldHandle.Index, newHandle.Index);
        Assert.NotEqual(oldHandle.Generation, newHandle.Generation);
        Assert.Throws<InvalidOperationException>(() => pool.Get(oldHandle));
        Assert.Same(second, pool.Get(newHandle));
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void RetirementQueueReleasesOnlyCompletedTimelineValues()
    {
        FrameRetirementQueue queue = new();
        List<string> released = [];
        queue.Retire(9, () => released.Add("nine"));
        queue.Retire(3, () => released.Add("three"));
        queue.Retire(6, () => released.Add("six"));

        Assert.Equal(0, queue.Collect(2));
        Assert.Equal(1, queue.Collect(3));
        Assert.Equal(["three"], released);
        Assert.Equal(1, queue.Collect(7));
        Assert.Equal(["three", "six"], released);
        Assert.Equal(1, queue.Collect(ulong.MaxValue));
        Assert.Equal(["three", "six", "nine"], released);
        Assert.Equal(0, queue.Count);
    }

    private readonly struct TextureTag;

    private sealed record TestResource(string Name);
}
