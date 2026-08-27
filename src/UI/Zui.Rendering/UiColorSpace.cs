namespace ZEngine.UI.Rendering;

public readonly record struct UiLinearColor(
    float Red,
    float Green,
    float Blue,
    float Alpha);

public static class UiColorSpace
{
    private static readonly float[] SrgbToLinearTable = CreateSrgbToLinearTable();

    public static float SrgbToLinear(byte component) =>
        SrgbToLinearTable[component];

    public static UiLinearColor ToPremultipliedLinear(UiColor color)
    {
        float alpha = color.Alpha / 255f;
        return new(
            SrgbToLinear(color.Red) * alpha,
            SrgbToLinear(color.Green) * alpha,
            SrgbToLinear(color.Blue) * alpha,
            alpha);
    }

    private static float[] CreateSrgbToLinearTable()
    {
        float[] values = new float[256];
        for (int index = 0; index < values.Length; index++)
        {
            float srgb = index / 255f;
            values[index] = srgb <= 0.04045f
                ? srgb / 12.92f
                : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
        }

        return values;
    }
}
