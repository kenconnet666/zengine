using System.ComponentModel;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using ZEngine.Agent;

namespace ZEngine.AgentHost;

[McpServerToolType]
public sealed class EngineMcpTools(AgentHostState state)
{
    [McpServerTool(Name = "engine_action_catalog")]
    [Description("List typed ZEngine agent actions and their required permissions.")]
    public IReadOnlyList<object> ActionCatalog() =>
        state.Actions.Catalog
            .Select(action => (object)new
            {
                action.Name,
                requestType = action.RequestType.FullName,
                resultType = action.ResultType.FullName,
                permission = action.RequiredPermission.ToString(),
                owner = action.Owner.Id,
                generation = action.Owner.Generation.Value
            })
            .ToArray();

    [McpServerTool(Name = "engine_echo")]
    [Description("Queue a typed engine mutation and return its frame-correlated receipt.")]
    public async Task<ActionReceipt<string>> Echo(
        [Description("Text returned by the engine action.")] string value,
        CancellationToken cancellationToken)
    {
        return await state.Dispatcher.Enqueue<string, string>(
            state.Session,
            "engine.echo",
            value,
            cancellationToken);
    }

    [McpServerTool(Name = "engine_ui_tree")]
    [Description("Return the current engine UI semantic tree with stable node ids and actions.")]
    public UiSemanticTree UiTree() => state.ObserveUi();

    [McpServerTool(Name = "engine_input")]
    [Description("Queue text input for a semantic UI node.")]
    public async Task<ActionReceipt<UiActionResponse>> Input(
        [Description("Stable UI node id.")] ulong nodeId,
        [Description("Text value to inject through the engine UI event route.")] string value,
        CancellationToken cancellationToken) =>
        await state.Dispatcher.Enqueue<UiInputRequest, UiActionResponse>(
            state.Session,
            "ui.input",
            new(nodeId, value),
            cancellationToken);

    [McpServerTool(Name = "engine_click")]
    [Description("Queue a semantic click for a UI node; does not inject native OS input.")]
    public async Task<ActionReceipt<UiActionResponse>> Click(
        [Description("Stable UI node id.")] ulong nodeId,
        CancellationToken cancellationToken) =>
        await state.Dispatcher.Enqueue<UiClickRequest, UiActionResponse>(
            state.Session,
            "ui.click",
            new(nodeId),
            cancellationToken);

    [McpServerTool(Name = "engine_wait")]
    [Description("Wait until the engine main-loop frame is greater than the supplied frame.")]
    public async Task<object> Wait(
        [Description("Previously observed or completed frame.")] ulong afterFrame,
        CancellationToken cancellationToken)
    {
        while (state.Dispatcher.CurrentFrame <= afterFrame)
        {
            await Task.Delay(5, cancellationToken);
        }

        return new { frame = state.Dispatcher.CurrentFrame };
    }

    [McpServerTool(Name = "engine_capture")]
    [Description("Capture the current engine surface and return MCP ImageContent plus frame metadata.")]
    public async Task<CallToolResult> Capture(CancellationToken cancellationToken)
    {
        ActionReceipt<FrameCaptureResponse> receipt =
            await state.Dispatcher.Enqueue<FrameCaptureRequest, FrameCaptureResponse>(
                state.Session,
                "frame.capture",
                new(),
                cancellationToken);
        return new()
        {
            Content =
            [
                ImageContentBlock.FromBytes(
                    receipt.CapturePng!,
                    "image/png")
            ],
            StructuredContent = JsonSerializer.SerializeToElement(new
            {
                receipt.RequestId,
                receipt.RequestedFrame,
                receipt.StartedFrame,
                receipt.CompletedFrame,
                receipt.Succeeded,
                receipt.Result,
                receipt.Deltas,
                receipt.Logs,
                receipt.Duration
            })
        };
    }
}
