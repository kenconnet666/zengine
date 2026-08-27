using System.Text;

namespace ZEngine.UI.Rendering;

public sealed record RasterizedGlyph(
    int Width,
    int Height,
    int BearingX,
    int BearingY,
    int Advance,
    byte[] Alpha);

public readonly record struct GlyphRasterizerTelemetry(
    long Requests,
    long CacheHits,
    long CacheMisses);

public interface IGlyphRasterizer
{
    GlyphRasterizerTelemetry Telemetry { get; }

    RasterizedGlyph Rasterize(Rune rune, int pixelSize, int fontWeight);
}
