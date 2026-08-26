namespace ZEngine.UI;

public enum UiTag
{
    Root,
    Div,
    Span,
    Text,
    Heading1,
    Button,
    Input
}

public enum UiRole
{
    None,
    Group,
    Heading,
    Button,
    TextBox,
    Label,
    Status
}

public enum UiEventType
{
    Click,
    Input,
    Change,
    Focus,
    Blur,
    KeyDown
}

public readonly record struct UiNodeId(ulong Value)
{
    public override string ToString() => $"ui-{Value:X16}";
}

public readonly record struct UiClickEvent(UiNodeId Target);

public readonly record struct UiInputEvent(UiNodeId Target, string Value);

public delegate void UiEventHandler<in TEvent>(TEvent @event);

public sealed class UiNode
{
    internal UiNode(UiNodeId id, UiTag tag)
    {
        Id = id;
        Tag = tag;
    }

    public UiNodeId Id { get; }

    public UiTag Tag { get; }

    public string? Key { get; internal set; }

    public string? Text { get; internal set; }

    public string? Value { get; internal set; }

    public string? Placeholder { get; internal set; }

    public string? SemanticLabel { get; internal set; }

    public UiRole Role { get; internal set; }

    public UiStyle Style { get; } = new();

    public List<UiNode> Children { get; } = [];

    public Dictionary<UiEventType, Delegate> Events { get; } = [];
}

public sealed record UiTree(
    UiNode Root,
    ulong Generation,
    UiTreeChange Change);

public sealed record UiTreeChange(
    int Added,
    int Removed,
    int Updated,
    bool ReusedPreviousTree)
{
    public static UiTreeChange Reused { get; } = new(0, 0, 0, true);
}
