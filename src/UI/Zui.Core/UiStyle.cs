namespace ZEngine.UI;

public enum UiDisplay
{
    Block,
    Flex,
    Grid,
    None
}

public enum FlexDirection
{
    Row,
    Column
}

public enum AlignItems
{
    Start,
    Center,
    End,
    Stretch
}

public enum JustifyContent
{
    Start,
    Center,
    End,
    SpaceBetween
}

public readonly record struct UiEdges(
    UiLength Top,
    UiLength Right,
    UiLength Bottom,
    UiLength Left)
{
    public static UiEdges All(UiLength value) =>
        new(value, value, value, value);
}

public readonly record struct UiShadow(
    float OffsetX,
    float OffsetY,
    float Blur,
    float Spread,
    UiColor Color);

public sealed record UiStyle
{
    public UiDisplay Display { get; set; } = UiDisplay.Block;
    public FlexDirection FlexDirection { get; set; } = FlexDirection.Column;
    public AlignItems AlignItems { get; set; } = AlignItems.Stretch;
    public JustifyContent JustifyContent { get; set; } = JustifyContent.Start;
    public UiLength Width { get; set; } = UiLength.Auto;
    public UiLength Height { get; set; } = UiLength.Auto;
    public UiLength MinWidth { get; set; } = UiLength.Auto;
    public UiLength MinHeight { get; set; } = UiLength.Auto;
    public UiEdges Padding { get; set; } = UiEdges.All(UiLength.Px(0));
    public UiEdges Margin { get; set; } = UiEdges.All(UiLength.Px(0));
    public UiLength Gap { get; set; } = UiLength.Px(0);
    public UiColor? BackgroundColor { get; set; }
    public UiColor? Color { get; set; }
    public UiColor? BorderColor { get; set; }
    public UiLength BorderWidth { get; set; } = UiLength.Px(0);
    public UiLength BorderRadius { get; set; } = UiLength.Px(0);
    public UiShadow? Shadow { get; set; }
    public float Opacity { get; set; } = 1;
    public float FontSize { get; set; } = 16;
    public int FontWeight { get; set; } = 400;
}
