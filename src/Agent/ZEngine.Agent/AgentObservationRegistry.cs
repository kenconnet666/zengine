namespace ZEngine.Agent;

public sealed record AgentObservationDescriptor(
    string Name,
    Type ResultType,
    AgentPermission RequiredPermission,
    AgentOwner Owner);

public sealed class AgentObservationRegistry
{
    private readonly Dictionary<string, IObservation> _observations = new(
        StringComparer.Ordinal);
    private readonly object _gate = new();

    public IDisposable Register<TResult>(
        AgentOwner owner,
        string name,
        AgentPermission permission,
        Func<AgentSession, TResult> observe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(observe);
        Observation<TResult> observation = new(
            new(name, typeof(TResult), permission, owner),
            observe);
        lock (_gate)
        {
            if (!_observations.TryAdd(name, observation))
            {
                throw new InvalidOperationException(
                    $"Agent observation '{name}' is already registered.");
            }
        }

        return new Registration(this, name, observation);
    }

    public TResult Observe<TResult>(AgentSession session, string name)
    {
        IObservation observation;
        lock (_gate)
        {
            observation = _observations.TryGetValue(name, out IObservation? found)
                ? found
                : throw new KeyNotFoundException(
                    $"Agent observation '{name}' is not registered.");
        }

        session.Demand(observation.Descriptor.RequiredPermission);
        return observation.Observe(session) is TResult typed
            ? typed
            : throw new InvalidCastException(
                $"Observation '{name}' does not return {typeof(TResult).FullName}.");
    }

    private void Remove(string name, IObservation expected)
    {
        lock (_gate)
        {
            if (_observations.TryGetValue(name, out IObservation? current)
                && ReferenceEquals(current, expected))
            {
                _observations.Remove(name);
            }
        }
    }

    private interface IObservation
    {
        AgentObservationDescriptor Descriptor { get; }

        object? Observe(AgentSession session);
    }

    private sealed class Observation<TResult>(
        AgentObservationDescriptor descriptor,
        Func<AgentSession, TResult> observe) : IObservation
    {
        public AgentObservationDescriptor Descriptor { get; } = descriptor;

        public object? Observe(AgentSession session) => observe(session);
    }

    private sealed class Registration(
        AgentObservationRegistry registry,
        string name,
        IObservation observation) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                registry.Remove(name, observation);
            }
        }
    }
}
