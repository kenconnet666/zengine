using ZEngine.Agent;
using ZEngine.Core;
using ZEngine.UI;
using ZEngine.UI.Basic;
using ZEngine.UI.Rendering;
using ZEngine.Graphics.Vulkan;

namespace ZEngine.AgentHost;

public sealed class AgentHostState : IDisposable
{
    private readonly IDisposable _echoRegistration;
    private readonly DemoComponent _component = new();
    private readonly DemoTheme _theme = new();
    private readonly UiAgentAdapter _ui;
    private readonly FrameCaptureAgentAdapter _capture;
    private long _frame;

    public AgentHostState()
    {
        Actions = new();
        Dispatcher = new(Actions);
        Session = new(
            "mcp-local",
            AgentPermission.Observe
            | AgentPermission.EngineInput
            | AgentPermission.EngineMutation
            | AgentPermission.Capture);
        _echoRegistration = Actions.Register<string, string>(
            new("agent-host", GenerationId.Initial),
            "engine.echo",
            AgentPermission.EngineMutation,
            static (value, _) => ValueTask.FromResult(
                new AgentActionResult<string>(value, [], [])));
        _ui = new(
            Actions,
            new("agent-host-ui", GenerationId.Initial),
            CurrentTree,
            CurrentLayout);
        _capture = new(
            Actions,
            new("agent-host-capture", GenerationId.Initial),
            static () => PngEncoder.EncodeBgra(
                new byte[]
                {
                    0, 0, 255, 255,
                    0, 255, 0, 255,
                    255, 0, 0, 255,
                    255, 255, 255, 255
                },
                2,
                2));
    }

    public AgentActionRegistry Actions { get; }

    public AgentDispatcher Dispatcher { get; }

    public AgentSession Session { get; }

    public ulong NextFrame() =>
        unchecked((ulong)Interlocked.Increment(ref _frame));

    public UiSemanticTree ObserveUi() => _ui.Observe(Session);

    public UiTree CurrentTree() => _component.Render(_theme);

    public UiLayout CurrentLayout() =>
        new UiLayoutEngine().Layout(CurrentTree(), 960, 640);

    public void Dispose()
    {
        _capture.Dispose();
        _ui.Dispose();
        _component.Dispose();
        _echoRegistration.Dispose();
    }

    private sealed class DemoTheme : SysTheme;

    private sealed class DemoComponent : Component<DemoTheme>
    {
        private readonly State<string> _value = new(string.Empty);
        private readonly State<int> _clicks = new(0);

        protected override void Compose(Ui<DemoTheme> ui)
        {
            ui.DIV(page =>
            {
                page.Semantics(UiRole.Group, "Agent Host UI");
                page.INPUT(input =>
                {
                    input.Semantics(UiRole.TextBox, "Agent value");
                    input.Value(_value.Value);
                    input.OnInput(inputEvent => _value.Value = inputEvent.Value);
                });
                page.BUTTON($"Click {_clicks.Value}", button =>
                {
                    button.Semantics(UiRole.Button, "Agent click");
                    button.OnClick(_ => _clicks.Value++);
                });
            });
        }
    }
}

public sealed class AgentFramePump(
    AgentHostState state,
    ILogger<AgentFramePump> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int drained = state.Dispatcher.Drain(state.NextFrame());
            if (drained > 0)
            {
                logger.LogDebug("Drained {Count} agent actions.", drained);
            }

            await Task.Delay(16, stoppingToken);
        }
    }
}
