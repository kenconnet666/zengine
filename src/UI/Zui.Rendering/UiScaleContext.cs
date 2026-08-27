namespace ZEngine.UI.Rendering;

public readonly record struct UiScaleContext
{
    private UiScaleContext(
        uint physicalWidth,
        uint physicalHeight,
        uint dpiX,
        uint dpiY,
        float scaleX,
        float scaleY)
    {
        ArgumentOutOfRangeException.ThrowIfZero(physicalWidth);
        ArgumentOutOfRangeException.ThrowIfZero(physicalHeight);
        ArgumentOutOfRangeException.ThrowIfZero(dpiX);
        ArgumentOutOfRangeException.ThrowIfZero(dpiY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaleX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaleY);

        PhysicalWidth = physicalWidth;
        PhysicalHeight = physicalHeight;
        DpiX = dpiX;
        DpiY = dpiY;
        ScaleX = scaleX;
        ScaleY = scaleY;
        LogicalWidth = physicalWidth / scaleX;
        LogicalHeight = physicalHeight / scaleY;
    }

    public uint PhysicalWidth { get; }

    public uint PhysicalHeight { get; }

    public uint DpiX { get; }

    public uint DpiY { get; }

    public float ScaleX { get; }

    public float ScaleY { get; }

    public float LogicalWidth { get; }

    public float LogicalHeight { get; }

    public static UiScaleContext FromDpi(
        uint physicalWidth,
        uint physicalHeight,
        uint dpiX,
        uint dpiY) =>
        new(
            physicalWidth,
            physicalHeight,
            dpiX,
            dpiY,
            dpiX / 96f,
            dpiY / 96f);

    public static UiScaleContext FromScale(
        uint physicalWidth,
        uint physicalHeight,
        float scale) =>
        new(
            physicalWidth,
            physicalHeight,
            checked((uint)MathF.Round(96 * scale)),
            checked((uint)MathF.Round(96 * scale)),
            scale,
            scale);

    public UiRect ToPhysicalBox(UiRect logical)
    {
        float left = Snap(logical.X * ScaleX);
        float top = Snap(logical.Y * ScaleY);
        float right = Snap((logical.X + logical.Width) * ScaleX);
        float bottom = Snap((logical.Y + logical.Height) * ScaleY);
        return new(
            left,
            top,
            Math.Max(0, right - left),
            Math.Max(0, bottom - top));
    }

    public UiRect ToPhysicalText(UiRect logical, float logicalFontSize)
    {
        UiRect box = ToPhysicalBox(logical);
        float physicalFontSize = logicalFontSize * ScaleY;
        float baseline = Snap((logical.Y + logicalFontSize) * ScaleY);
        return box with { Y = baseline - physicalFontSize };
    }

    public float ScaleHorizontal(float logical) => logical * ScaleX;

    public float ScaleVertical(float logical) => logical * ScaleY;

    public float ScaleUniform(float logical) =>
        logical * ((ScaleX + ScaleY) * 0.5f);

    public float SnapStroke(float logical)
    {
        if (logical <= 0)
        {
            return 0;
        }

        return Math.Max(1, Snap(ScaleUniform(logical)));
    }

    private static float Snap(float value) =>
        MathF.Round(value, MidpointRounding.AwayFromZero);
}
