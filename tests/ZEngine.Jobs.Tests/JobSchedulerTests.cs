using System.Collections.Concurrent;
using ZEngine.Core;
using Xunit;

namespace ZEngine.Jobs.Tests;

public sealed class JobSchedulerTests
{
    [Fact]
    public void RunsDependenciesInOrderAndPropagatesOwnerGeneration()
    {
        using JobScheduler scheduler = new(workerCount: 4);
        using JobGroup group = scheduler.CreateGroup(new(42));
        ConcurrentQueue<(int Step, GenerationId Generation)> events = new();

        JobHandle first = group.Schedule(new RecordJob(events, 1));
        JobHandle second = group.Schedule(
            new RecordJob(events, 2),
            [first]);
        JobHandle third = group.Schedule(
            new RecordJob(events, 3),
            [second]);
        third.Complete();

        Assert.Equal([1, 2, 3], events.Select(item => item.Step));
        Assert.All(events, item => Assert.Equal(new GenerationId(42), item.Generation));
        Assert.Equal(0, group.ActiveJobs);
    }

    [Fact]
    public async Task ExecutesManyJobsAcrossFixedWorkers()
    {
        using JobScheduler scheduler = new(workerCount: 4);
        using JobGroup group = scheduler.CreateGroup(GenerationId.Initial);
        Counter counter = new();
        JobHandle[] handles = new JobHandle[4_000];
        for (int index = 0; index < handles.Length; index++)
        {
            handles[index] = group.Schedule(new IncrementJob(counter));
        }

        await Task.WhenAll(handles.Select(handle => handle.AsTask()));
        Assert.Equal(4_000, counter.Value);
        Assert.Equal(4, scheduler.WorkerCount);
    }

    [Fact]
    public void PropagatesDependencyFailureWithoutRunningConsumer()
    {
        using JobScheduler scheduler = new(workerCount: 2);
        using JobGroup group = scheduler.CreateGroup(GenerationId.Initial);
        Counter counter = new();
        JobHandle failed = group.Schedule(new ThrowJob());
        JobHandle dependent = group.Schedule(
            new IncrementJob(counter),
            [failed]);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(dependent.Complete);
        Assert.Contains("dependency", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, counter.Value);
    }

    [Fact]
    public async Task QuiesceCancelsWorkRejectsNewJobsAndReachesIdle()
    {
        using JobScheduler scheduler = new(workerCount: 2);
        using JobGroup group = scheduler.CreateGroup(new(7));
        ManualResetEventSlim started = new();
        JobHandle handle = group.Schedule(new WaitForCancellationJob(started));
        Assert.True(started.Wait(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken));

        group.Quiesce();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await handle.AsTask());
        Assert.True(await group.WaitForIdleAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() =>
            group.Schedule(new IncrementJob(new Counter())));
    }

    private readonly struct RecordJob(
        ConcurrentQueue<(int Step, GenerationId Generation)> events,
        int step) : IJob
    {
        public void Execute(in JobContext context) =>
            events.Enqueue((step, context.OwnerGeneration));
    }

    private readonly struct IncrementJob(Counter counter) : IJob
    {
        public void Execute(in JobContext context) =>
            Interlocked.Increment(ref counter.Value);
    }

    private readonly struct ThrowJob : IJob
    {
        public void Execute(in JobContext context) =>
            throw new TestException("expected job failure");
    }

    private readonly struct WaitForCancellationJob(ManualResetEventSlim started)
        : IJob
    {
        public void Execute(in JobContext context)
        {
            started.Set();
            context.CancellationToken.WaitHandle.WaitOne();
            context.CancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class Counter
    {
        public int Value;
    }

    private sealed class TestException(string message) : Exception(message);
}
