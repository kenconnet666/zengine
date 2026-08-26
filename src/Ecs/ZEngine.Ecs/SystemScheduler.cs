using ZEngine.Jobs;

namespace ZEngine.Ecs;

public readonly record struct FrameTime(
    TimeSpan FixedDelta,
    TimeSpan VariableDelta,
    ulong FrameIndex);

public interface IEcsSystem
{
    void Update(World world, in FrameTime time);
}

public readonly record struct SystemAccess(ulong Reads, ulong Writes)
{
    public static SystemAccess None { get; } = new(0, 0);

    public SystemAccess Read<T>()
        where T : unmanaged
    {
        ulong bit = 1ul << ComponentSchema<T>.Value.RuntimeIndex;
        return new(Reads | bit, Writes);
    }

    public SystemAccess Write<T>()
        where T : unmanaged
    {
        ulong bit = 1ul << ComponentSchema<T>.Value.RuntimeIndex;
        return new(Reads & ~bit, Writes | bit);
    }

    public bool ConflictsWith(in SystemAccess other) =>
        (Writes & (other.Reads | other.Writes)) != 0
        || (other.Writes & Reads) != 0;
}

public sealed class EcsSystemScheduler
{
    private readonly List<SystemRegistration> _systems = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    public void Add(
        string name,
        IEcsSystem system,
        in SystemAccess access)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(system);
        if (!_names.Add(name))
        {
            throw new InvalidOperationException(
                $"ECS system name '{name}' is already registered.");
        }

        _systems.Add(new(name, system, access));
    }

    public EcsSystemPlan Compile()
    {
        List<List<SystemRegistration>> batches = [];
        foreach (SystemRegistration system in _systems)
        {
            int targetBatch = -1;
            for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                if (batches[batchIndex].TrueForAll(candidate =>
                        !candidate.Access.ConflictsWith(system.Access)))
                {
                    targetBatch = batchIndex;
                    break;
                }
            }

            if (targetBatch < 0)
            {
                targetBatch = batches.Count;
                batches.Add([]);
            }

            batches[targetBatch].Add(system);
        }

        return new(
            batches
                .Select(batch => batch.ToArray())
                .ToArray());
    }
}

public sealed class EcsSystemPlan
{
    private readonly SystemRegistration[][] _batches;
    private readonly JobHandle[][] _handles;

    internal EcsSystemPlan(SystemRegistration[][] batches)
    {
        _batches = batches;
        _handles = batches
            .Select(batch => new JobHandle[batch.Length])
            .ToArray();
        Batches = batches
            .Select((batch, index) => new EcsSystemBatch(
                index,
                batch.Select(system => system.Name).ToArray()))
            .ToArray();
    }

    public IReadOnlyList<EcsSystemBatch> Batches { get; }

    public void Execute(
        World world,
        in FrameTime time,
        JobGroup jobs)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(jobs);
        for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
        {
            SystemRegistration[] batch = _batches[batchIndex];
            JobHandle[] handles = _handles[batchIndex];
            for (int systemIndex = 0; systemIndex < batch.Length; systemIndex++)
            {
                handles[systemIndex] = jobs.Schedule(
                    new SystemJob(batch[systemIndex], world, time));
            }

            for (int systemIndex = 0; systemIndex < handles.Length; systemIndex++)
            {
                handles[systemIndex].Complete();
            }
        }
    }

    private readonly struct SystemJob(
        SystemRegistration registration,
        World world,
        FrameTime time) : IJob
    {
        public void Execute(in JobContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            registration.System.Update(world, time);
        }
    }
}

public sealed record EcsSystemBatch(
    int Index,
    IReadOnlyList<string> Systems);

internal sealed record SystemRegistration(
    string Name,
    IEcsSystem System,
    SystemAccess Access);
