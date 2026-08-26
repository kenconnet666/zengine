using System.Collections.Concurrent;
using System.Diagnostics;

namespace ZEngine.Agent;

public sealed class AgentDispatcher
{
    private readonly AgentActionRegistry _registry;
    private readonly ConcurrentQueue<PendingAction> _pending = new();
    private long _currentFrame;

    public AgentDispatcher(AgentActionRegistry registry)
    {
        _registry = registry;
    }

    public ulong CurrentFrame => unchecked((ulong)Volatile.Read(ref _currentFrame));

    public int PendingCount => _pending.Count;

    public Task<ActionReceipt<TResult>> Enqueue<TRequest, TResult>(
        AgentSession session,
        string action,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        IActionInvoker invoker = _registry.Get(action);
        if (invoker.Descriptor.RequestType != typeof(TRequest)
            || invoker.Descriptor.ResultType != typeof(TResult))
        {
            throw new ArgumentException(
                $"Typed action signature does not match catalog entry '{action}'.");
        }

        PendingAction<TRequest, TResult> pending = new(
            Guid.NewGuid(),
            CurrentFrame,
            session,
            invoker,
            request!,
            cancellationToken);
        _pending.Enqueue(pending);
        return pending.Task;
    }

    public int Drain(ulong frame, int maximumActions = 256)
    {
        Volatile.Write(ref _currentFrame, checked((long)frame));
        int executed = 0;
        while (executed < maximumActions
               && _pending.TryDequeue(out PendingAction? pending))
        {
            pending.Execute(frame);
            executed++;
        }

        return executed;
    }
}

internal abstract class PendingAction
{
    public abstract void Execute(ulong frame);
}

internal sealed class PendingAction<TRequest, TResult> : PendingAction
{
    private readonly Guid _id;
    private readonly ulong _requestedFrame;
    private readonly AgentSession _session;
    private readonly IActionInvoker _invoker;
    private readonly TRequest _request;
    private readonly CancellationToken _cancellationToken;
    private readonly TaskCompletionSource<ActionReceipt<TResult>> _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public PendingAction(
        Guid id,
        ulong requestedFrame,
        AgentSession session,
        IActionInvoker invoker,
        TRequest request,
        CancellationToken cancellationToken)
    {
        _id = id;
        _requestedFrame = requestedFrame;
        _session = session;
        _invoker = invoker;
        _request = request;
        _cancellationToken = cancellationToken;
    }

    public Task<ActionReceipt<TResult>> Task => _completion.Task;

    public override void Execute(ulong frame)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            object boxed = _invoker.Invoke(
                    _request!,
                    new(_session, frame, _cancellationToken))
                .AsTask()
                .GetAwaiter()
                .GetResult();
            var result = (AgentActionResult<TResult>)boxed;
            _completion.TrySetResult(new(
                _id,
                _invoker.Descriptor.Name,
                _requestedFrame,
                frame,
                frame,
                true,
                result.Result,
                null,
                result.Deltas,
                result.Logs,
                stopwatch.Elapsed,
                result.CapturePng));
        }
        catch (Exception exception)
        {
            _completion.TrySetResult(new(
                _id,
                _invoker.Descriptor.Name,
                _requestedFrame,
                frame,
                frame,
                false,
                default,
                exception.Message,
                [],
                [],
                stopwatch.Elapsed,
                null));
        }
    }
}
