using System.Collections.Concurrent;
using ZEngine.Core;

namespace ZEngine.Jobs;

public sealed class JobScheduler : IDisposable
{
    private readonly ConcurrentQueue<JobNode>[] _queues;
    private readonly Thread[] _workers;
    private readonly SemaphoreSlim _workSignal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private int _nextQueue;
    private bool _disposed;

    public JobScheduler(int? workerCount = null)
    {
        int count = workerCount
                    ?? Math.Max(1, Environment.ProcessorCount - 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        WorkerCount = count;
        _queues = new ConcurrentQueue<JobNode>[count];
        _workers = new Thread[count];
        for (int index = 0; index < count; index++)
        {
            _queues[index] = new();
        }

        for (int index = 0; index < count; index++)
        {
            int workerIndex = index;
            _workers[index] = new(() => WorkerLoop(workerIndex))
            {
                IsBackground = true,
                Name = $"ZEngine.JobWorker.{index}"
            };
            _workers[index].Start();
        }
    }

    public int WorkerCount { get; }

    public JobGroup CreateGroup(GenerationId generation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (generation.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        return new(this, generation);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _workSignal.Release(_workers.Length);
        foreach (Thread worker in _workers)
        {
            worker.Join();
        }

        _shutdown.Dispose();
        _workSignal.Dispose();
    }

    internal JobHandle Schedule<TJob>(
        JobGroup group,
        in TJob job,
        ReadOnlySpan<JobHandle> dependencies)
        where TJob : struct, IJob
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancellationToken cancellation = group.RegisterJob();
        JobNode node = new(
            this,
            group,
            new JobBox<TJob>(job),
            cancellation);
        try
        {
            for (int index = 0; index < dependencies.Length; index++)
            {
                JobNode? dependency = dependencies[index].Node;
                if (dependency is not null)
                {
                    node.AddDependency(dependency);
                }
            }

            node.FinishDependencyRegistration();
            return new(node);
        }
        catch
        {
            node.FailRegistration();
            throw;
        }
    }

    internal void Enqueue(JobNode node)
    {
        int queueIndex = (int)((uint)Interlocked.Increment(ref _nextQueue)
                               % (uint)_queues.Length);
        _queues[queueIndex].Enqueue(node);
        _workSignal.Release();
    }

    private void WorkerLoop(int workerIndex)
    {
        CancellationToken cancellation = _shutdown.Token;
        while (!cancellation.IsCancellationRequested)
        {
            if (TryTakeWork(workerIndex, out JobNode? node))
            {
                node!.Run();
                continue;
            }

            try
            {
                _workSignal.Wait(cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private bool TryTakeWork(int workerIndex, out JobNode? node)
    {
        if (_queues[workerIndex].TryDequeue(out node))
        {
            return true;
        }

        for (int offset = 1; offset < _queues.Length; offset++)
        {
            int stealIndex = (workerIndex + offset) % _queues.Length;
            if (_queues[stealIndex].TryDequeue(out node))
            {
                return true;
            }
        }

        node = null;
        return false;
    }
}

internal interface IJobBox
{
    void Execute(in JobContext context);
}

internal sealed class JobBox<TJob>(TJob job) : IJobBox
    where TJob : struct, IJob
{
    private TJob _job = job;

    public void Execute(in JobContext context) => _job.Execute(context);
}

internal sealed class JobNode
{
    private readonly JobScheduler _scheduler;
    private readonly JobGroup _group;
    private readonly IJobBox _job;
    private readonly CancellationToken _cancellation;
    private readonly object _gate = new();
    private readonly List<JobNode> _dependents = [];
    private readonly TaskCompletionSource _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _remainingDependencies;
    private int _queued;
    private bool _dependenciesRegistered;
    private bool _completed;
    private Exception? _completionError;
    private Exception? _dependencyFailure;

    public JobNode(
        JobScheduler scheduler,
        JobGroup group,
        IJobBox job,
        CancellationToken cancellation)
    {
        _scheduler = scheduler;
        _group = group;
        _job = job;
        _cancellation = cancellation;
    }

    public Task Completion => _completion.Task;

    public void AddDependency(JobNode dependency)
    {
        Interlocked.Increment(ref _remainingDependencies);
        if (!dependency.TryAddDependent(this, out Exception? failure))
        {
            if (failure is not null)
            {
                RecordDependencyFailure(failure);
            }

            Interlocked.Decrement(ref _remainingDependencies);
        }
    }

    public void FinishDependencyRegistration()
    {
        Volatile.Write(ref _dependenciesRegistered, true);
        TryQueueIfReady();
    }

    public void FailRegistration()
    {
        Complete(new InvalidOperationException("Job dependency registration failed."));
    }

    public void Run()
    {
        Exception? error = _dependencyFailure;
        if (error is null)
        {
            try
            {
                _cancellation.ThrowIfCancellationRequested();
                JobContext context = new(
                    _group.Generation,
                    _cancellation);
                _job.Execute(context);
            }
            catch (Exception exception)
            {
                error = exception;
            }
        }

        Complete(error);
    }

    private bool TryAddDependent(
        JobNode dependent,
        out Exception? failure)
    {
        lock (_gate)
        {
            if (_completed)
            {
                failure = _completionError;
                return false;
            }

            _dependents.Add(dependent);
            failure = null;
            return true;
        }
    }

    private void DependencyCompleted(Exception? error)
    {
        if (error is not null)
        {
            RecordDependencyFailure(error);
        }

        Interlocked.Decrement(ref _remainingDependencies);
        TryQueueIfReady();
    }

    private void RecordDependencyFailure(Exception error) =>
        Interlocked.CompareExchange(
            ref _dependencyFailure,
            new InvalidOperationException(
                "A job dependency failed.",
                error),
            null);

    private void TryQueueIfReady()
    {
        if (Volatile.Read(ref _dependenciesRegistered)
            && Volatile.Read(ref _remainingDependencies) == 0
            && Interlocked.CompareExchange(ref _queued, 1, 0) == 0)
        {
            _scheduler.Enqueue(this);
        }
    }

    private void Complete(Exception? error)
    {
        JobNode[] dependents;
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _completionError = error;
            _completed = true;
            dependents = _dependents.ToArray();
            _dependents.Clear();
        }

        if (error is null)
        {
            _completion.TrySetResult();
        }
        else if (error is OperationCanceledException cancellation)
        {
            _completion.TrySetCanceled(cancellation.CancellationToken);
        }
        else
        {
            _completion.TrySetException(error);
        }

        foreach (JobNode dependent in dependents)
        {
            dependent.DependencyCompleted(error);
        }

        _group.CompleteJob();
    }
}
