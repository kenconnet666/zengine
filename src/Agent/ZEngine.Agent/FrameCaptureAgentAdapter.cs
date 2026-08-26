namespace ZEngine.Agent;

public readonly record struct FrameCaptureRequest(string Surface = "main");

public sealed record FrameCaptureResponse(
    string Surface,
    int ByteLength);

public sealed class FrameCaptureAgentAdapter : IDisposable
{
    private readonly Func<byte[]> _capture;
    private readonly IDisposable _registration;

    public FrameCaptureAgentAdapter(
        AgentActionRegistry registry,
        AgentOwner owner,
        Func<byte[]> capture)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(capture);
        _capture = capture;
        _registration = registry.Register<FrameCaptureRequest, FrameCaptureResponse>(
            owner,
            "frame.capture",
            AgentPermission.Capture,
            HandleCapture);
    }

    public void Dispose() => _registration.Dispose();

    private ValueTask<AgentActionResult<FrameCaptureResponse>> HandleCapture(
        FrameCaptureRequest request,
        AgentActionContext context)
    {
        byte[] png = _capture();
        return ValueTask.FromResult(
            new AgentActionResult<FrameCaptureResponse>(
                new(request.Surface, png.Length),
                [new("frame.capture", new Dictionary<string, string>
                {
                    ["surface"] = request.Surface,
                    ["bytes"] = png.Length.ToString()
                })],
                [],
                png));
    }
}
