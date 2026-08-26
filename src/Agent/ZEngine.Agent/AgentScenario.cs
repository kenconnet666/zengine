namespace ZEngine.Agent;

public sealed class AgentScenario
{
    private readonly List<IScenarioStep> _steps = [];

    public AgentScenario(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }

    public AgentScenario Step<TRequest, TResult>(
        string action,
        TRequest request,
        Action<ActionReceipt<TResult>>? verify = null)
    {
        _steps.Add(new ScenarioStep<TRequest, TResult>(action, request, verify));
        return this;
    }

    public async Task<IReadOnlyList<object>> RunAsync(
        AgentDispatcher dispatcher,
        AgentSession session,
        ulong startingFrame = 1,
        CancellationToken cancellationToken = default)
    {
        List<object> receipts = [];
        ulong frame = startingFrame;
        foreach (IScenarioStep step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task<object> receipt = step.Enqueue(
                dispatcher,
                session,
                cancellationToken);
            dispatcher.Drain(frame++);
            receipts.Add(await receipt);
        }

        return receipts;
    }

    private interface IScenarioStep
    {
        Task<object> Enqueue(
            AgentDispatcher dispatcher,
            AgentSession session,
            CancellationToken cancellationToken);
    }

    private sealed class ScenarioStep<TRequest, TResult>(
        string action,
        TRequest request,
        Action<ActionReceipt<TResult>>? verify) : IScenarioStep
    {
        public async Task<object> Enqueue(
            AgentDispatcher dispatcher,
            AgentSession session,
            CancellationToken cancellationToken)
        {
            ActionReceipt<TResult> receipt = await dispatcher.Enqueue<TRequest, TResult>(
                session,
                action,
                request,
                cancellationToken);
            verify?.Invoke(receipt);
            return receipt;
        }
    }
}
