using ZEngine.CounterPlugin.Contracts;
using ZEngine.Jobs;
using ZEngine.Plugins;

namespace ZEngine.CounterPlugin.Runtime;

public sealed class CounterPlugin : EnginePlugin
{
    private CounterService? _service;

    public override void Configure(PluginScope scope)
    {
        _service = new(scope);
        scope.Export<ICounterService>(_service);
        scope.Retire(new Retirement(_service));
    }

    public override void SaveHotState(HotStateWriter state) =>
        state.Write("counter.value", _service?.Value ?? 0);

    public override void RestoreHotState(HotStateReader state)
    {
        if (_service is not null)
        {
            _service.Value = state.ReadOr("counter.value", 0);
        }
    }

    private sealed class CounterService(PluginScope scope)
        : ICounterService, IDisposable
    {
        private readonly ManualResetEventSlim _release = new();
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int Value { get; set; }

        public bool IsDisposed { get; private set; }

        public bool ResourceRetired { get; set; }

        public Task WorkStarted => _started.Task;

        public void StartBlockingWork() =>
            scope.Schedule(new BlockingJob(this));

        public void ReleaseWork() => _release.Set();

        public void Dispose()
        {
            IsDisposed = true;
            _release.Set();
            _release.Dispose();
        }

        private readonly struct BlockingJob(CounterService service) : IJob
        {
            public void Execute(in JobContext context)
            {
                service._started.TrySetResult();
                WaitHandle.WaitAny(
                    [service._release.WaitHandle, context.CancellationToken.WaitHandle]);
                context.CancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private sealed class Retirement(CounterService service) : IPluginRetirement
    {
        public async ValueTask RetireAsync()
        {
            await Task.Yield();
            service.ResourceRetired = true;
        }
    }
}
