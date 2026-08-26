using ZEngine.UI;
using ZEngine.UI.Rendering;

namespace ZEngine.Agent;

public sealed record UiSemanticNode(
    ulong NodeId,
    UiRole Role,
    string? Label,
    string? Text,
    UiRect? Bounds,
    IReadOnlyList<string> Actions,
    IReadOnlyList<UiSemanticNode> Children);

public sealed record UiSemanticTree(
    ulong UiGeneration,
    UiSemanticNode Root);

public sealed record UiClickRequest(ulong NodeId);

public sealed record UiInputRequest(ulong NodeId, string Value);

public sealed record UiActionResponse(
    ulong NodeId,
    string Event,
    bool Handled);

public sealed class UiAgentAdapter : IDisposable
{
    private readonly Func<UiTree> _treeProvider;
    private readonly Func<UiLayout?> _layoutProvider;
    private readonly IDisposable _clickRegistration;
    private readonly IDisposable _inputRegistration;

    public UiAgentAdapter(
        AgentActionRegistry registry,
        AgentOwner owner,
        Func<UiTree> treeProvider,
        Func<UiLayout?>? layoutProvider = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(treeProvider);
        _treeProvider = treeProvider;
        _layoutProvider = layoutProvider ?? (static () => null);
        _clickRegistration = registry.Register<UiClickRequest, UiActionResponse>(
            owner,
            "ui.click",
            AgentPermission.EngineInput,
            HandleClick);
        _inputRegistration = registry.Register<UiInputRequest, UiActionResponse>(
            owner,
            "ui.input",
            AgentPermission.EngineInput,
            HandleInput);
    }

    public UiSemanticTree Observe(AgentSession session)
    {
        session.Demand(AgentPermission.Observe);
        UiTree tree = _treeProvider();
        UiLayout? layout = _layoutProvider();
        return new(
            tree.Generation,
            BuildSemantic(tree.Root, layout));
    }

    public void Dispose()
    {
        _inputRegistration.Dispose();
        _clickRegistration.Dispose();
    }

    private ValueTask<AgentActionResult<UiActionResponse>> HandleClick(
        UiClickRequest request,
        AgentActionContext context)
    {
        UiNode node = Find(_treeProvider().Root, request.NodeId);
        bool handled = node.Events.TryGetValue(
            UiEventType.Click,
            out Delegate? callback);
        if (handled)
        {
            ((UiEventHandler<UiClickEvent>)callback!)(new(node.Id));
        }

        return ValueTask.FromResult(new AgentActionResult<UiActionResponse>(
            new(request.NodeId, "click", handled),
            [new("ui.event", new Dictionary<string, string>
            {
                ["nodeId"] = request.NodeId.ToString(),
                ["event"] = "click",
                ["handled"] = handled.ToString()
            })],
            []));
    }

    private ValueTask<AgentActionResult<UiActionResponse>> HandleInput(
        UiInputRequest request,
        AgentActionContext context)
    {
        UiNode node = Find(_treeProvider().Root, request.NodeId);
        bool handled = node.Events.TryGetValue(
            UiEventType.Input,
            out Delegate? callback);
        if (handled)
        {
            ((UiEventHandler<UiInputEvent>)callback!)(new(
                node.Id,
                request.Value));
        }

        return ValueTask.FromResult(new AgentActionResult<UiActionResponse>(
            new(request.NodeId, "input", handled),
            [new("ui.event", new Dictionary<string, string>
            {
                ["nodeId"] = request.NodeId.ToString(),
                ["event"] = "input",
                ["handled"] = handled.ToString(),
                ["valueLength"] = request.Value.Length.ToString()
            })],
            []));
    }

    private static UiSemanticNode BuildSemantic(
        UiNode node,
        UiLayout? layout)
    {
        List<string> actions = [];
        if (node.Events.ContainsKey(UiEventType.Click))
        {
            actions.Add("ui.click");
        }

        if (node.Events.ContainsKey(UiEventType.Input))
        {
            actions.Add("ui.input");
        }

        UiRect? bounds = layout is not null
                         && layout.Bounds.TryGetValue(node.Id, out UiRect found)
            ? found
            : null;
        return new(
            node.Id.Value,
            node.Role,
            node.SemanticLabel,
            node.Text,
            bounds,
            actions,
            node.Children.Select(child => BuildSemantic(child, layout)).ToArray());
    }

    private static UiNode Find(UiNode node, ulong nodeId)
    {
        if (node.Id.Value == nodeId)
        {
            return node;
        }

        foreach (UiNode child in node.Children)
        {
            UiNode? found = FindOrNull(child, nodeId);
            if (found is not null)
            {
                return found;
            }
        }

        throw new KeyNotFoundException($"UI node 0x{nodeId:X16} is not active.");
    }

    private static UiNode? FindOrNull(UiNode node, ulong nodeId)
    {
        if (node.Id.Value == nodeId)
        {
            return node;
        }

        foreach (UiNode child in node.Children)
        {
            UiNode? found = FindOrNull(child, nodeId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
