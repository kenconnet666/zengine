using ZEngine.Core;

namespace ZEngine.Agent;

[Flags]
public enum AgentPermission
{
    None = 0,
    Observe = 1 << 0,
    EngineInput = 1 << 1,
    EngineMutation = 1 << 2,
    Capture = 1 << 3,
    NativeInput = 1 << 4
}

public sealed record AgentSession(
    string Id,
    AgentPermission Permissions)
{
    public void Demand(AgentPermission permission)
    {
        if ((Permissions & permission) != permission)
        {
            throw new UnauthorizedAccessException(
                $"Agent session '{Id}' lacks permission {permission}.");
        }
    }
}

public sealed record AgentOwner(
    string Id,
    GenerationId Generation);

public sealed record AgentDelta(
    string Kind,
    IReadOnlyDictionary<string, string> Values);

public sealed record AgentActionResult<TResult>(
    TResult Result,
    IReadOnlyList<AgentDelta> Deltas,
    IReadOnlyList<string> Logs,
    byte[]? CapturePng = null);

public sealed record ActionReceipt<TResult>(
    Guid RequestId,
    string Action,
    ulong RequestedFrame,
    ulong StartedFrame,
    ulong CompletedFrame,
    bool Succeeded,
    TResult? Result,
    string? Error,
    IReadOnlyList<AgentDelta> Deltas,
    IReadOnlyList<string> Logs,
    TimeSpan Duration,
    byte[]? CapturePng);

public readonly record struct AgentActionContext(
    AgentSession Session,
    ulong Frame,
    CancellationToken CancellationToken);

public delegate ValueTask<AgentActionResult<TResult>> AgentActionHandler<TRequest, TResult>(
    TRequest request,
    AgentActionContext context);
