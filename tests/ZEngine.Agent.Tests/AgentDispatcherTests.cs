using UiLab.Shared;
using Xunit;
using ZEngine.Core;
using ZEngine.UI;

namespace ZEngine.Agent.Tests;

public sealed class AgentDispatcherTests
{
    [Fact]
    public async Task MutationExecutesOnlyWhenMainThreadDrainsFrame()
    {
        AgentActionRegistry registry = new();
        int value = 0;
        using IDisposable registration = registry.Register<int, int>(
            new("engine", GenerationId.Initial),
            "counter.add",
            AgentPermission.EngineMutation,
            (amount, context) =>
            {
                value += amount;
                return ValueTask.FromResult(new AgentActionResult<int>(
                    value,
                    [new("counter", new Dictionary<string, string>
                    {
                        ["value"] = value.ToString()
                    })],
                    []));
            });
        AgentDispatcher dispatcher = new(registry);
        AgentSession session = new(
            "test",
            AgentPermission.Observe | AgentPermission.EngineMutation);
        Task<ActionReceipt<int>> pending = dispatcher.Enqueue<int, int>(
            session,
            "counter.add",
            3,
            TestContext.Current.CancellationToken);

        Assert.False(pending.IsCompleted);
        Assert.Equal(0, value);
        Assert.Equal(1, dispatcher.Drain(42));
        ActionReceipt<int> receipt = await pending;

        Assert.True(receipt.Succeeded);
        Assert.Equal(3, receipt.Result);
        Assert.Equal(42ul, receipt.StartedFrame);
        Assert.Equal(42ul, receipt.CompletedFrame);
        Assert.Equal(3, value);
    }

    [Fact]
    public async Task UiSemanticActionsShareNodeIdsAndUpdateState()
    {
        using LoginCard component = new();
        AppTheme theme = new();
        UiTree tree = component.Render(theme);
        AgentActionRegistry registry = new();
        AgentDispatcher dispatcher = new(registry);
        using UiAgentAdapter ui = new(
            registry,
            new("zui.basic", GenerationId.Initial),
            () => component.Render(theme));
        AgentSession session = new(
            "ui-test",
            AgentPermission.Observe | AgentPermission.EngineInput);
        UiSemanticTree semantics = ui.Observe(session);
        UiSemanticNode input = Find(semantics.Root, UiRole.TextBox);
        UiSemanticNode button = Find(semantics.Root, UiRole.Button);
        Assert.Contains("ui.input", input.Actions);
        Assert.Contains("ui.click", button.Actions);

        Task<ActionReceipt<UiActionResponse>> inputReceipt =
            dispatcher.Enqueue<UiInputRequest, UiActionResponse>(
                session,
                "ui.input",
                new(input.NodeId, "Agent用户"),
                TestContext.Current.CancellationToken);
        dispatcher.Drain(10);
        Assert.True((await inputReceipt).Succeeded);
        Task<ActionReceipt<UiActionResponse>> clickReceipt =
            dispatcher.Enqueue<UiClickRequest, UiActionResponse>(
                session,
                "ui.click",
                new(button.NodeId),
                TestContext.Current.CancellationToken);
        dispatcher.Drain(11);
        Assert.True((await clickReceipt).Succeeded);

        UiTree updated = component.Render(theme);
        Assert.Contains("Agent用户 已点击登录 1 次", FlattenText(updated.Root));
        Assert.Equal(input.NodeId, Find(ui.Observe(session).Root, UiRole.TextBox).NodeId);
    }

    [Fact]
    public async Task ScenarioAndScopedCatalogRemovalAreDeterministic()
    {
        AgentActionRegistry actions = new();
        AgentObservationRegistry observations = new();
        int value = 0;
        IDisposable actionRegistration = actions.Register<int, int>(
            new("plugin.test", new(7)),
            "value.set",
            AgentPermission.EngineMutation,
            (next, _) =>
            {
                value = next;
                return ValueTask.FromResult(new AgentActionResult<int>(
                    value,
                    [],
                    []));
            });
        IDisposable observationRegistration = observations.Register(
            new("plugin.test", new(7)),
            "value.get",
            AgentPermission.Observe,
            _ => value);
        AgentDispatcher dispatcher = new(actions);
        AgentSession session = new(
            "scenario",
            AgentPermission.Observe | AgentPermission.EngineMutation);
        AgentScenario scenario = new AgentScenario("set value")
            .Step<int, int>(
                "value.set",
                9,
                receipt => Assert.Equal(9, receipt.Result));

        IReadOnlyList<object> receipts = await scenario.RunAsync(
            dispatcher,
            session,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(receipts);
        Assert.Equal(9, observations.Observe<int>(session, "value.get"));

        actionRegistration.Dispose();
        observationRegistration.Dispose();
        Assert.Empty(actions.Catalog);
        Assert.Throws<KeyNotFoundException>(() =>
            observations.Observe<int>(session, "value.get"));
    }

    [Fact]
    public async Task CaptureActionReturnsPngInStructuredReceipt()
    {
        AgentActionRegistry registry = new();
        byte[] expected = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];
        using FrameCaptureAgentAdapter capture = new(
            registry,
            new("vulkan.capture", GenerationId.Initial),
            () => expected);
        AgentDispatcher dispatcher = new(registry);
        AgentSession session = new(
            "capture",
            AgentPermission.Capture);
        Task<ActionReceipt<FrameCaptureResponse>> pending =
            dispatcher.Enqueue<FrameCaptureRequest, FrameCaptureResponse>(
                session,
                "frame.capture",
                new(),
                TestContext.Current.CancellationToken);

        dispatcher.Drain(77);
        ActionReceipt<FrameCaptureResponse> receipt = await pending;

        Assert.True(receipt.Succeeded);
        Assert.Equal(expected, receipt.CapturePng);
        Assert.Equal(expected.Length, receipt.Result?.ByteLength);
        Assert.Equal(77ul, receipt.CompletedFrame);

        AgentSession engineOnly = new("engine-only", AgentPermission.EngineInput);
        Assert.Throws<UnauthorizedAccessException>(() =>
            engineOnly.Demand(AgentPermission.NativeInput));
    }

    private static UiSemanticNode Find(UiSemanticNode node, UiRole role)
    {
        if (node.Role == role)
        {
            return node;
        }

        foreach (UiSemanticNode child in node.Children)
        {
            try
            {
                return Find(child, role);
            }
            catch (KeyNotFoundException)
            {
            }
        }

        throw new KeyNotFoundException();
    }

    private static string FlattenText(UiNode node) =>
        (node.Text ?? string.Empty)
        + string.Concat(node.Children.Select(FlattenText));
}
