using ZEngine.Graphics.Vulkan;
using ZEngine.UI.Rendering;
using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;
using System.Text;

namespace ZEngine.UI.Vulkan;

public readonly record struct UiQuad(
    float X,
    float Y,
    float Width,
    float Height,
    UiColor Color);

public sealed class VulkanUiScene
{
    private UiQuad[] _quads = [];

    public IReadOnlyList<UiQuad> Quads => Volatile.Read(ref _quads);

    public void Update(
        IReadOnlyList<UiPaintCommand> commands,
        IGlyphRasterizer? glyphRasterizer = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        List<UiQuad> quads = [];
        foreach (UiPaintCommand command in commands)
        {
            if (command is PaintBox box)
            {
                if (box.Shadow is { } shadow)
                {
                    quads.Add(new(
                        box.Bounds.X + shadow.OffsetX - shadow.Spread,
                        box.Bounds.Y + shadow.OffsetY - shadow.Spread,
                        box.Bounds.Width + shadow.Spread * 2,
                        box.Bounds.Height + shadow.Spread * 2,
                        shadow.Color));
                }

                if (box.Border is { } border && box.BorderWidth > 0)
                {
                    quads.Add(new(
                        box.Bounds.X,
                        box.Bounds.Y,
                        box.Bounds.Width,
                        box.Bounds.Height,
                        border));
                    quads.Add(new(
                        box.Bounds.X + box.BorderWidth,
                        box.Bounds.Y + box.BorderWidth,
                        Math.Max(0, box.Bounds.Width - box.BorderWidth * 2),
                        Math.Max(0, box.Bounds.Height - box.BorderWidth * 2),
                        box.Color));
                }
                else
                {
                    quads.Add(new(
                        box.Bounds.X,
                        box.Bounds.Y,
                        box.Bounds.Width,
                        box.Bounds.Height,
                        box.Color));
                }
            }
            else if (command is PaintText text && glyphRasterizer is not null)
            {
                AddTextQuads(quads, text, glyphRasterizer);
            }
        }

        Volatile.Write(ref _quads, quads.ToArray());
    }

    private static void AddTextQuads(
        List<UiQuad> quads,
        PaintText text,
        IGlyphRasterizer rasterizer)
    {
        float cursorX = text.Bounds.X;
        float baseline = text.Bounds.Y + text.FontSize;
        foreach (Rune rune in text.Text.EnumerateRunes())
        {
            RasterizedGlyph glyph = rasterizer.Rasterize(
                rune,
                Math.Max(1, (int)MathF.Ceiling(text.FontSize)),
                text.FontWeight);
            float glyphX = cursorX + glyph.BearingX;
            float glyphY = baseline - glyph.BearingY;
            for (int y = 0; y < glyph.Height; y++)
            {
                int x = 0;
                while (x < glyph.Width)
                {
                    byte alpha = glyph.Alpha[y * glyph.Width + x];
                    if (alpha < 16)
                    {
                        x++;
                        continue;
                    }

                    int runStart = x;
                    byte runAlpha = alpha;
                    while (x < glyph.Width
                           && Math.Abs(
                               glyph.Alpha[y * glyph.Width + x]
                               - runAlpha) < 24)
                    {
                        x++;
                    }

                    quads.Add(new(
                        glyphX + runStart,
                        glyphY + y,
                        x - runStart,
                        1,
                        text.Color with
                        {
                            Alpha = (byte)(text.Color.Alpha * runAlpha / 255)
                        }));
                }
            }

            cursorX += glyph.Advance;
        }
    }
}

public sealed class VulkanUiOverlayFactory : IVulkanOverlayFactory
{
    private readonly VulkanUiScene _scene;
    private readonly byte[] _vertexShader;
    private readonly byte[] _fragmentShader;

    public VulkanUiOverlayFactory(
        VulkanUiScene scene,
        ReadOnlySpan<byte> vertexShader,
        ReadOnlySpan<byte> fragmentShader)
    {
        ArgumentNullException.ThrowIfNull(scene);
        SpirvModule.Validate(vertexShader, nameof(vertexShader));
        SpirvModule.Validate(fragmentShader, nameof(fragmentShader));
        _scene = scene;
        _vertexShader = vertexShader.ToArray();
        _fragmentShader = fragmentShader.ToArray();
    }

    public IVulkanOverlay Create(VulkanDevice device, VkFormat colorFormat) =>
        new VulkanUiOverlay(
            device,
            colorFormat,
            _scene,
            _vertexShader,
            _fragmentShader);
}
