using ZEngine.Core;
using ZEngine.Jobs;

namespace ZEngine.Plugins;

public sealed class PluginScope : IAsyncDisposable
{
    private readonly Dictionary<Type, object> _services = [];
    private readonly List<IDisposable> _owned = [];
    private readonly JobGroup _jobs;
    private readonly object _gate = new();
    private bool _accepting = true;
    private bool _disposed;

    public PluginScope(
        string pluginId,
        GenerationId generation,
        JobGroup jobs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(jobs);
        PluginId = pluginId;
        Generation = generation;
        _jobs = jobs;
    }

    public string PluginId { get; }

    public GenerationId Generation { get; }

    public IReadOnlyDictionary<Type, object> Services => _services;

    public TService Export<TService>(TService service)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(service);
        EnsureAccepting();
        Type contract = typeof(TService);
        if (!_services.TryAdd(contract, service))
        {
            throw new InvalidOperationException(
                $"Plugin {PluginId} already exports {contract.FullName}.");
        }

        if (service is IDisposable disposable)
        {
            _owned.Add(disposable);
        }

        return service;
    }

    public void Own(IDisposable contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        EnsureAccepting();
        _owned.Add(contribution);
    }

    public JobHandle Schedule<TJob>(
        in TJob job,
        ReadOnlySpan<JobHandle> dependencies = default)
        where TJob : struct, IJob
    {
        EnsureAccepting();
        return _jobs.Schedule(job, dependencies);
    }

    public void Suspend()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _accepting = false;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_jobs.IsAccepting)
            {
                throw new InvalidOperationException(
                    "A final-quiesced plugin scope cannot resume.");
            }

            _accepting = true;
        }
    }

    public Task WaitForIdleAsync() => _jobs.WaitForIdleAsync();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        Suspend();
        _jobs.Quiesce();
        await _jobs.WaitForIdleAsync();
        for (int index = _owned.Count - 1; index >= 0; index--)
        {
            _owned[index].Dispose();
        }

        _owned.Clear();
        _services.Clear();
        _jobs.Dispose();
        _disposed = true;
    }

    private void EnsureAccepting()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_accepting)
            {
                throw new InvalidOperationException(
                    $"Plugin {PluginId} generation {Generation} rejects new contributions and jobs.");
            }
        }
    }
}
