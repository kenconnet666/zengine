namespace ZEngine.UI;

public readonly record struct UiColor(byte Red, byte Green, byte Blue, byte Alpha = 255)
{
    public static UiColor FromRgb(uint rgb) =>
        new(
            (byte)(rgb >> 16),
            (byte)(rgb >> 8),
            (byte)rgb);

    public string ToCss() => Alpha == 255
        ? $"#{Red:X2}{Green:X2}{Blue:X2}"
        : $"rgba({Red}, {Green}, {Blue}, {Alpha / 255d:0.###})";
}

public enum LengthUnit
{
    Auto,
    Pixels,
    Percent,
    Rem,
    Fraction
}

public readonly record struct UiLength(float Value, LengthUnit Unit)
{
    public static UiLength Auto { get; } = new(0, LengthUnit.Auto);

    public static UiLength Px(float value) => new(value, LengthUnit.Pixels);

    public static UiLength Percent(float value) => new(value, LengthUnit.Percent);

    public static UiLength Rem(float value) => new(value, LengthUnit.Rem);

    public static UiLength Fr(float value) => new(value, LengthUnit.Fraction);

    public string ToCss() => Unit switch
    {
        LengthUnit.Auto => "auto",
        LengthUnit.Pixels => $"{Value:0.###}px",
        LengthUnit.Percent => $"{Value:0.###}%",
        LengthUnit.Rem => $"{Value:0.###}rem",
        LengthUnit.Fraction => $"{Value:0.###}fr",
        _ => throw new ArgumentOutOfRangeException()
    };
}

public sealed record ThemeColors(
    UiColor Page,
    UiColor Surface,
    UiColor Primary,
    UiColor PrimaryText,
    UiColor Text,
    UiColor MutedText,
    UiColor Border,
    UiColor Danger);

public class SysTheme
{
    public virtual ThemeColors Colors { get; } = new(
        UiColor.FromRgb(0x10141C),
        UiColor.FromRgb(0x1A202B),
        UiColor.FromRgb(0x5B8CFF),
        UiColor.FromRgb(0xFFFFFF),
        UiColor.FromRgb(0xF3F6FC),
        UiColor.FromRgb(0xAAB4C3),
        UiColor.FromRgb(0x313A49),
        UiColor.FromRgb(0xF06464));
}
