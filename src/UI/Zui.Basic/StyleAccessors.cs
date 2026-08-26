namespace ZEngine.UI.Basic;

public enum LengthProperty
{
    Width,
    Height,
    Gap
}

public enum EdgeProperty
{
    Padding,
    Margin
}

public readonly struct LengthStyle(
    UiStyle style,
    LengthProperty property)
{
    public void Auto() => Set(UiLength.Auto);

    public void Px(float value) => Set(UiLength.Px(value));

    public void Percent(float value) => Set(UiLength.Percent(value));

    public void Full() => Set(UiLength.Percent(100));

    public void Rem(float value) => Set(UiLength.Rem(value));

    private void Set(UiLength value)
    {
        switch (property)
        {
            case LengthProperty.Width:
                style.Width = value;
                break;
            case LengthProperty.Height:
                style.Height = value;
                break;
            case LengthProperty.Gap:
                style.Gap = value;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public readonly struct EdgeStyle(
    UiStyle style,
    EdgeProperty property)
{
    public void All(float pixels) =>
        Set(UiEdges.All(UiLength.Px(pixels)));

    public void XY(float horizontal, float vertical) =>
        Set(new(
            UiLength.Px(vertical),
            UiLength.Px(horizontal),
            UiLength.Px(vertical),
            UiLength.Px(horizontal)));

    public void Each(
        float top,
        float right,
        float bottom,
        float left) =>
        Set(new(
            UiLength.Px(top),
            UiLength.Px(right),
            UiLength.Px(bottom),
            UiLength.Px(left)));

    private void Set(UiEdges value)
    {
        if (property == EdgeProperty.Padding)
        {
            style.Padding = value;
        }
        else
        {
            style.Margin = value;
        }
    }
}

public readonly struct BackgroundColorStyle<TTheme>(
    UiStyle style,
    TTheme theme)
    where TTheme : SysTheme
{
    public void Page() => style.BackgroundColor = theme.Colors.Page;

    public void Surface() => style.BackgroundColor = theme.Colors.Surface;

    public void Primary() => style.BackgroundColor = theme.Colors.Primary;

    public void Danger() => style.BackgroundColor = theme.Colors.Danger;

    public void Set(UiColor color) => style.BackgroundColor = color;
}

public readonly struct ForegroundColorStyle<TTheme>(
    UiStyle style,
    TTheme theme)
    where TTheme : SysTheme
{
    public void Text() => style.Color = theme.Colors.Text;

    public void Muted() => style.Color = theme.Colors.MutedText;

    public void PrimaryText() => style.Color = theme.Colors.PrimaryText;

    public void Set(UiColor color) => style.Color = color;
}

public readonly struct BorderStyle<TTheme>(
    UiStyle style,
    TTheme theme)
    where TTheme : SysTheme
{
    public BorderStyle<TTheme> Width(float pixels)
    {
        style.BorderWidth = UiLength.Px(pixels);
        return this;
    }

    public BorderStyle<TTheme> Radius(float pixels)
    {
        style.BorderRadius = UiLength.Px(pixels);
        return this;
    }

    public BorderStyle<TTheme> DefaultColor()
    {
        style.BorderColor = theme.Colors.Border;
        return this;
    }

    public BorderStyle<TTheme> Color(UiColor color)
    {
        style.BorderColor = color;
        return this;
    }
}

public readonly struct ShadowStyle(UiStyle style)
{
    public void None() => style.Shadow = null;

    public void Medium() => style.Shadow = new(
        0,
        8,
        24,
        0,
        new(0, 0, 0, 90));

    public void Set(UiShadow shadow) => style.Shadow = shadow;
}

public readonly struct DisplayStyle(UiStyle style)
{
    public void Block() => style.Display = UiDisplay.Block;

    public void Flex() => style.Display = UiDisplay.Flex;

    public void Grid() => style.Display = UiDisplay.Grid;

    public void None() => style.Display = UiDisplay.None;
}

public readonly struct FlexStyle(UiStyle style)
{
    public FlexStyle Row()
    {
        style.Display = UiDisplay.Flex;
        style.FlexDirection = FlexDirection.Row;
        return this;
    }

    public FlexStyle Column()
    {
        style.Display = UiDisplay.Flex;
        style.FlexDirection = FlexDirection.Column;
        return this;
    }

    public FlexStyle Align(AlignItems value)
    {
        style.AlignItems = value;
        return this;
    }

    public FlexStyle Justify(JustifyContent value)
    {
        style.JustifyContent = value;
        return this;
    }
}

public readonly struct TypographyStyle(UiStyle style)
{
    public TypographyStyle Size(float pixels)
    {
        style.FontSize = pixels;
        return this;
    }

    public TypographyStyle Weight(int value)
    {
        style.FontWeight = value;
        return this;
    }
}
