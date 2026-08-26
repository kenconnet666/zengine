using System.Collections.Concurrent;
using ZEngine.Core;
using ZEngine.Jobs;
using Xunit;

namespace ZEngine.Ecs.Tests;

public sealed class SystemSchedulerTests
{
    [Fact]
    public void RunsNonConflictingSystemsInOneParallelBatch()
    {
        using JobScheduler jobs = new(workerCount: 2);
        using JobGroup group = jobs.CreateGroup(GenerationId.Initial);
        CountdownEvent rendezvous = new(2);
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        EcsSystemScheduler scheduler = new();
        scheduler.Add(
            "Parallel.A",
            new RendezvousSystem(rendezvous, cancellation),
            SystemAccess.None.Write<ComponentA>());
        scheduler.Add(
            "Parallel.B",
            new RendezvousSystem(rendezvous, cancellation),
            SystemAccess.None.Write<ComponentB>());
        EcsSystemPlan plan = scheduler.Compile();

        EcsSystemBatch batch = Assert.Single(plan.Batches);
        Assert.Equal(["Parallel.A", "Parallel.B"], batch.Systems);
        plan.Execute(
            new(),
            new(TimeSpan.FromSeconds(1.0 / 60), TimeSpan.Zero, 1),
            group);
        Assert.Equal(0, rendezvous.CurrentCount);
    }

    [Fact]
    public void SeparatesConflictingSystemsAndPreservesBarrierOrder()
    {
        using JobScheduler jobs = new(workerCount: 2);
        using JobGroup group = jobs.CreateGroup(GenerationId.Initial);
        ConcurrentQueue<string> order = new();
        EcsSystemScheduler scheduler = new();
        scheduler.Add(
            "Write.A",
            new RecordSystem(order, "write"),
            SystemAccess.None.Write<ComponentA>());
        scheduler.Add(
            "Read.A",
            new RecordSystem(order, "read"),
            SystemAccess.None.Read<ComponentA>());
        EcsSystemPlan plan = scheduler.Compile();

        Assert.Equal(2, plan.Batches.Count);
        plan.Execute(
            new(),
            new(TimeSpan.FromSeconds(1.0 / 60), TimeSpan.Zero, 1),
            group);
        Assert.Equal(["write", "read"], order);
    }

    private sealed class RendezvousSystem(
        CountdownEvent rendezvous,
        CancellationToken cancellation) : IEcsSystem
    {
        public void Update(World world, in FrameTime time)
        {
            rendezvous.Signal();
            if (!rendezvous.Wait(TimeSpan.FromSeconds(2), cancellation))
            {
                throw new TimeoutException(
                    "Systems expected in one batch did not execute concurrently.");
            }
        }
    }

    private sealed class RecordSystem(
        ConcurrentQueue<string> order,
        string value) : IEcsSystem
    {
        public void Update(World world, in FrameTime time) =>
            order.Enqueue(value);
    }

    [Component]
    private readonly struct ComponentA;

    [Component]
    private readonly struct ComponentB;
}
