namespace ZEngine.Agent;

public sealed class AgentActionRegistry
{
    private readonly Dictionary<string, IActionInvoker> _actions = new(
        StringComparer.Ordinal);
    private readonly object _gate = new();

    public IReadOnlyList<AgentActionDescriptor> Catalog
    {
        get
        {
            lock (_gate)
            {
                return _actions
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Value.Descriptor)
                    .ToArray();
            }
        }
    }

    public IDisposable Register<TRequest, TResult>(
        AgentOwner owner,
        string name,
        AgentPermission requiredPermission,
        AgentActionHandler<TRequest, TResult> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        ActionInvoker<TRequest, TResult> invoker = new(
            new(
                name,
                typeof(TRequest),
                typeof(TResult),
                requiredPermission,
                owner),
            handler);
        lock (_gate)
        {
            if (!_actions.TryAdd(name, invoker))
            {
                throw new InvalidOperationException(
                    $"Agent action '{name}' is already registered.");
            }
        }

        return new Registration(this, name, invoker);
    }

    internal IActionInvoker Get(string name)
    {
        lock (_gate)
        {
            return _actions.TryGetValue(name, out IActionInvoker? action)
                ? action
                : throw new KeyNotFoundException(
                    $"Agent action '{name}' is not registered.");
        }
    }

    private void Remove(string name, IActionInvoker expected)
    {
        lock (_gate)
        {
            if (_actions.TryGetValue(name, out IActionInvoker? current)
                && ReferenceEquals(current, expected))
            {
                _actions.Remove(name);
            }
        }
    }

    private sealed class Registration(
        AgentActionRegistry registry,
        string name,
        IActionInvoker invoker) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                registry.Remove(name, invoker);
            }
        }
    }
}

public sealed record AgentActionDescriptor(
    string Name,
    Type RequestType,
    Type ResultType,
    AgentPermission RequiredPermission,
    AgentOwner Owner);

internal interface IActionInvoker
{
    AgentActionDescriptor Descriptor { get; }

    ValueTask<object> Invoke(object request, AgentActionContext context);
}

internal sealed class ActionInvoker<TRequest, TResult>(
    AgentActionDescriptor descriptor,
    AgentActionHandler<TRequest, TResult> handler) : IActionInvoker
{
    public AgentActionDescriptor Descriptor { get; } = descriptor;

    public async ValueTask<object> Invoke(
        object request,
        AgentActionContext context)
    {
        context.Session.Demand(Descriptor.RequiredPermission);
        if (request is not TRequest typed)
        {
            throw new ArgumentException(
                $"Action {Descriptor.Name} expected {typeof(TRequest).FullName}.");
        }

        return await handler(typed, context);
    }
}
