using ZEngine.Graphics.Vulkan;
using ZEngine.UI.Rendering;
using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;
using System.Diagnostics;
using System.Text;

namespace ZEngine.UI.Vulkan;

public readonly record struct UiQuad(
    float X,
    float Y,
    float Width,
    float Height,
    UiColor Color);

public readonly record struct UiRenderTelemetry(
    int PaintCommandCount,
    int BoxInstances,
    int GlyphInstances,
    int InstanceCount,
    int DrawCount,
    long GlyphRequests,
    long GlyphCacheHits,
    long GlyphCacheMisses,
    long AtlasUploadBytes,
    double SceneCompileMilliseconds);

public sealed class VulkanUiScene
{
    private UiQuad[] _quads = [];
    private UiRenderTelemetry _telemetry;

    public IReadOnlyList<UiQuad> Quads => Volatile.Read(ref _quads);

    public UiRenderTelemetry Telemetry => _telemetry;

    public void Update(
        IReadOnlyList<UiPaintCommand> commands,
        IGlyphRasterizer? glyphRasterizer = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        long started = Stopwatch.GetTimestamp();
        List<UiQuad> quads = [];
        int boxInstances = 0;
        int glyphInstances = 0;
        foreach (UiPaintCommand command in commands)
        {
            if (command is PaintBox box)
            {
                int before = quads.Count;
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

                boxInstances += quads.Count - before;
            }
            else if (command is PaintText text && glyphRasterizer is not null)
            {
                int before = quads.Count;
                AddTextQuads(quads, text, glyphRasterizer);
                glyphInstances += quads.Count - before;
            }
        }

        UiQuad[] compiled = quads.ToArray();
        GlyphRasterizerTelemetry glyphTelemetry =
            glyphRasterizer?.Telemetry ?? default;
        _telemetry = new(
            commands.Count,
            boxInstances,
            glyphInstances,
            compiled.Length,
            compiled.Length,
            glyphTelemetry.Requests,
            glyphTelemetry.CacheHits,
            glyphTelemetry.CacheMisses,
            0,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        Volatile.Write(ref _quads, compiled);
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
