using System.Text;

namespace ZEngine.UI.Rendering;

public sealed record RasterizedGlyph(
    int Width,
    int Height,
    int BearingX,
    int BearingY,
    int Advance,
    byte[] Alpha);

public interface IGlyphRasterizer
{
    RasterizedGlyph Rasterize(Rune rune, int pixelSize, int fontWeight);
}
