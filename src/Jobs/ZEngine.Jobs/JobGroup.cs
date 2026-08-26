using ZEngine.Core;

namespace ZEngine.Jobs;

public sealed class JobGroup : IDisposable
{
    private readonly JobScheduler _scheduler;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private TaskCompletionSource _idle = CompletedSource();
    private int _activeJobs;
    private bool _accepting = true;
    private bool _disposed;

    internal JobGroup(
        JobScheduler scheduler,
        GenerationId generation)
    {
        _scheduler = scheduler;
        Generation = generation;
    }

    public GenerationId Generation { get; }

    public int ActiveJobs => Volatile.Read(ref _activeJobs);

    public bool IsAccepting
    {
        get
        {
            lock (_gate)
            {
                return _accepting;
            }
        }
    }

    public JobHandle Schedule<TJob>(
        in TJob job,
        ReadOnlySpan<JobHandle> dependencies = default)
        where TJob : struct, IJob =>
        _scheduler.Schedule(this, job, dependencies);

    public void Quiesce(bool cancelPending = true)
    {
        lock (_gate)
        {
            _accepting = false;
            if (cancelPending)
            {
                _cancellation.Cancel();
            }
        }
    }

    public Task WaitForIdleAsync() => IdleTask();

    public async Task<bool> WaitForIdleAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await IdleTask().WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Quiesce();
        if (ActiveJobs == 0)
        {
            _cancellation.Dispose();
        }
        else
        {
            _ = IdleTask().ContinueWith(
                static (_, state) =>
                    ((CancellationTokenSource)state!).Dispose(),
                _cancellation,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    internal CancellationToken RegisterJob()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_accepting)
            {
                throw new InvalidOperationException(
                    $"Job group generation {Generation} is quiescing and rejects new work.");
            }

            if (_activeJobs++ == 0)
            {
                _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return _cancellation.Token;
        }
    }

    internal void CompleteJob()
    {
        TaskCompletionSource? completed = null;
        lock (_gate)
        {
            _activeJobs--;
            if (_activeJobs == 0)
            {
                completed = _idle;
            }
        }

        completed?.TrySetResult();
    }

    private Task IdleTask()
    {
        lock (_gate)
        {
            return _idle.Task;
        }
    }

    private static TaskCompletionSource CompletedSource()
    {
        TaskCompletionSource source = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
