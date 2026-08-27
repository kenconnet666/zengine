using System.Text;

namespace ZEngine.UI.Rendering;

public readonly record struct UiRect(float X, float Y, float Width, float Height);

public sealed class UiLayout
{
    private readonly Dictionary<UiNodeId, UiRect> _bounds = [];

    public IReadOnlyDictionary<UiNodeId, UiRect> Bounds => _bounds;

    internal void Set(UiNodeId id, UiRect bounds) => _bounds[id] = bounds;

    public UiRect Get(UiNodeId id) => _bounds[id];
}

public sealed class UiLayoutEngine
{
    public UiLayout Layout(UiTree tree, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        UiLayout layout = new();
        float cursorY = 0;
        foreach (UiNode child in tree.Root.Children)
        {
            UiRect bounds = LayoutNode(
                child,
                0,
                cursorY,
                width,
                height,
                layout);
            cursorY += bounds.Height;
        }

        layout.Set(tree.Root.Id, new(0, 0, width, Math.Max(height, cursorY)));
        return layout;
    }

    private static UiRect LayoutNode(
        UiNode node,
        float x,
        float y,
        float availableWidth,
        float availableHeight,
        UiLayout layout)
    {
        UiStyle style = node.Style;
        float marginLeft = Pixels(style.Margin.Left, availableWidth);
        float marginRight = Pixels(style.Margin.Right, availableWidth);
        float marginTop = Pixels(style.Margin.Top, availableHeight);
        float marginBottom = Pixels(style.Margin.Bottom, availableHeight);
        float width = Resolve(
            style.Width,
            Math.Max(0, availableWidth - marginLeft - marginRight),
            availableWidth);
        float paddingLeft = Pixels(style.Padding.Left, width);
        float paddingRight = Pixels(style.Padding.Right, width);
        float paddingTop = Pixels(style.Padding.Top, availableHeight);
        float paddingBottom = Pixels(style.Padding.Bottom, availableHeight);
        float contentWidth = Math.Max(0, width - paddingLeft - paddingRight);
        float gap = Pixels(style.Gap, width);
        float contentHeight = node.Text is null
            ? 0
            : Math.Max(style.FontSize * 1.25f, 1);

        if (style.Display == UiDisplay.Flex
            && style.FlexDirection == FlexDirection.Row)
        {
            float cursorX = x + marginLeft + paddingLeft;
            float maximumHeight = contentHeight;
            for (int index = 0; index < node.Children.Count; index++)
            {
                UiNode child = node.Children[index];
                float remaining = Math.Max(0, contentWidth - (cursorX - x));
                float childAvailable = child.Style.Width.Unit == LengthUnit.Auto
                    ? Math.Min(remaining, IntrinsicWidth(child))
                    : remaining;
                UiRect childBounds = LayoutNode(
                    child,
                    cursorX,
                    y + marginTop + paddingTop,
                    childAvailable,
                    availableHeight,
                    layout);
                cursorX += childBounds.Width + gap;
                maximumHeight = Math.Max(maximumHeight, childBounds.Height);
            }

            contentHeight = maximumHeight;
        }
        else
        {
            float cursorY = y + marginTop + paddingTop + contentHeight;
            for (int index = 0; index < node.Children.Count; index++)
            {
                if (index > 0 || contentHeight > 0)
                {
                    cursorY += gap;
                }

                UiNode child = node.Children[index];
                float childWidth = child.Style.Width.Unit == LengthUnit.Auto
                    ? contentWidth
                    : Resolve(child.Style.Width, contentWidth, contentWidth);
                float childX = style.AlignItems switch
                {
                    AlignItems.Center =>
                        x + marginLeft + paddingLeft + (contentWidth - childWidth) / 2,
                    AlignItems.End =>
                        x + marginLeft + paddingLeft + contentWidth - childWidth,
                    _ => x + marginLeft + paddingLeft
                };
                UiRect childBounds = LayoutNode(
                    child,
                    childX,
                    cursorY,
                    contentWidth,
                    availableHeight,
                    layout);
                cursorY += childBounds.Height;
            }

            contentHeight = Math.Max(
                contentHeight,
                cursorY - (y + marginTop + paddingTop));
        }

        float height = style.Height.Unit == LengthUnit.Auto
            ? paddingTop + contentHeight + paddingBottom
            : Resolve(style.Height, availableHeight, availableHeight);
        if (height <= 0)
        {
            height = style.FontSize * 1.25f + paddingTop + paddingBottom;
        }

        UiRect result = new(
            x + marginLeft,
            y + marginTop,
            width,
            height);
        layout.Set(node.Id, result);
        return result with { Height = height + marginTop + marginBottom };
    }

    private static float Resolve(
        UiLength length,
        float auto,
        float reference) =>
        length.Unit switch
        {
            LengthUnit.Auto => auto,
            LengthUnit.Pixels => length.Value,
            LengthUnit.Percent => reference * length.Value / 100,
            LengthUnit.Rem => length.Value * 16,
            LengthUnit.Fraction => auto,
            _ => auto
        };

    private static float Pixels(UiLength length, float reference) =>
        Resolve(length, 0, reference);

    private static float IntrinsicWidth(UiNode node)
    {
        float textWidth = 0;
        if (node.Text is { } text)
        {
            int runes = 0;
            foreach (Rune _ in text.EnumerateRunes())
            {
                runes++;
            }

            textWidth = runes * node.Style.FontSize * 0.65f;
        }

        float childWidth = node.Children.Count == 0
            ? 0
            : node.Style.FlexDirection == FlexDirection.Row
                ? node.Children.Sum(IntrinsicWidth)
                : node.Children.Max(IntrinsicWidth);
        return Math.Max(textWidth, childWidth)
               + Pixels(node.Style.Padding.Left, 0)
               + Pixels(node.Style.Padding.Right, 0);
    }
}

public abstract record UiPaintCommand(UiNodeId Node);

public sealed record PaintBox(
    UiNodeId Node,
    UiRect Bounds,
    UiColor Color,
    UiColor? Border,
    float BorderWidth,
    float BorderRadius,
    UiShadow? Shadow) : UiPaintCommand(Node);

public sealed record PaintText(
    UiNodeId Node,
    UiRect Bounds,
    string Text,
    UiColor Color,
    float FontSize,
    int FontWeight) : UiPaintCommand(Node);

public sealed class UiPaintCompiler
{
    public IReadOnlyList<UiPaintCommand> Compile(UiTree tree, UiLayout layout)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(layout);
        List<UiPaintCommand> commands = [];
        foreach (UiNode child in tree.Root.Children)
        {
            CompileNode(child, layout, null, commands);
        }

        return commands;
    }

    public IReadOnlyList<UiPaintCommand> Compile(
        UiTree tree,
        UiLayout layout,
        UiScaleContext scale)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(layout);
        List<UiPaintCommand> commands = [];
        foreach (UiNode child in tree.Root.Children)
        {
            CompileNode(child, layout, scale, commands);
        }

        return commands;
    }

    private static void CompileNode(
        UiNode node,
        UiLayout layout,
        UiScaleContext? scale,
        List<UiPaintCommand> commands)
    {
        UiRect bounds = layout.Get(node.Id);
        if (node.Style.BackgroundColor is { } background)
        {
            UiRect paintBounds = scale is { } boxScale
                ? boxScale.ToPhysicalBox(bounds)
                : bounds;
            UiShadow? shadow = node.Style.Shadow;
            if (shadow is { } logicalShadow && scale is { } shadowScale)
            {
                shadow = new(
                    shadowScale.ScaleHorizontal(logicalShadow.OffsetX),
                    shadowScale.ScaleVertical(logicalShadow.OffsetY),
                    shadowScale.ScaleUniform(logicalShadow.Blur),
                    shadowScale.ScaleUniform(logicalShadow.Spread),
                    logicalShadow.Color);
            }

            commands.Add(new PaintBox(
                node.Id,
                paintBounds,
                background,
                node.Style.BorderColor,
                scale?.SnapStroke(node.Style.BorderWidth.Value)
                    ?? node.Style.BorderWidth.Value,
                scale?.ScaleUniform(node.Style.BorderRadius.Value)
                    ?? node.Style.BorderRadius.Value,
                shadow));
        }

        if (node.Text is { Length: > 0 } text)
        {
            UiRect textBounds = scale is { } textScale
                ? textScale.ToPhysicalText(bounds, node.Style.FontSize)
                : bounds;
            commands.Add(new PaintText(
                node.Id,
                textBounds,
                text,
                node.Style.Color ?? UiColor.FromRgb(0xFFFFFF),
                scale?.ScaleVertical(node.Style.FontSize)
                    ?? node.Style.FontSize,
                node.Style.FontWeight));
        }

        foreach (UiNode child in node.Children)
        {
            CompileNode(child, layout, scale, commands);
        }
    }
}
