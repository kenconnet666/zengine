using ZEngine.Core;

namespace ZEngine.Jobs;

public interface IJob
{
    void Execute(in JobContext context);
}

public readonly record struct JobContext(
    GenerationId OwnerGeneration,
    CancellationToken CancellationToken);

public readonly struct JobHandle
{
    private readonly JobNode? _node;

    internal JobHandle(JobNode node)
    {
        _node = node;
    }

    public bool IsValid => _node is not null;

    public bool IsCompleted => _node?.Completion.IsCompleted ?? true;

    internal JobNode? Node => _node;

    public Task AsTask() => _node?.Completion ?? Task.CompletedTask;

    public void Complete() => AsTask().GetAwaiter().GetResult();
}
