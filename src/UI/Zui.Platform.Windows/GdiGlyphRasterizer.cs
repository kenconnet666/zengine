using System.Runtime.InteropServices;
using System.Text;
using ZEngine.UI.Rendering;

namespace ZEngine.UI.Windows;

public sealed unsafe partial class GdiGlyphRasterizer : IGlyphRasterizer, IDisposable
{
    private const uint Gray8Bitmap = 6;
    private const uint GdiError = uint.MaxValue;
    private readonly string _family;
    private readonly Dictionary<(int CodePoint, int Size, int Weight), RasterizedGlyph>
        _cache = [];
    private nint _deviceContext;
    private long _requests;
    private long _cacheHits;
    private long _cacheMisses;

    public GdiGlyphRasterizer(string family = "Microsoft YaHei UI")
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        _family = family;
        _deviceContext = CreateCompatibleDC(0);
        if (_deviceContext == 0)
        {
            throw new InvalidOperationException("CreateCompatibleDC failed.");
        }
    }

    public RasterizedGlyph Rasterize(
        Rune rune,
        int pixelSize,
        int fontWeight)
    {
        ObjectDisposedException.ThrowIf(_deviceContext == 0, this);
        if (!rune.IsBmp)
        {
            throw new NotSupportedException(
                "The Win32 P4 glyph baseline currently accepts BMP runes; supplementary planes require DirectWrite glyph indexing.");
        }

        _requests++;
        if (_cache.TryGetValue(
            (rune.Value, pixelSize, fontWeight),
            out RasterizedGlyph? cached))
        {
            _cacheHits++;
            return cached;
        }

        _cacheMisses++;
        return _cache[(rune.Value, pixelSize, fontWeight)] = RasterizeCore(
            rune.Value,
            pixelSize,
            fontWeight);
    }

    public GlyphRasterizerTelemetry Telemetry =>
        new(_requests, _cacheHits, _cacheMisses);

    public void Dispose()
    {
        nint context = _deviceContext;
        if (context != 0)
        {
            _deviceContext = 0;
            _ = DeleteDC(context);
        }
    }

    private RasterizedGlyph RasterizeCore(
        int codePoint,
        int pixelSize,
        int fontWeight)
    {
        nint font = CreateFont(
            -pixelSize,
            0,
            0,
            0,
            fontWeight,
            0,
            0,
            0,
            1,
            0,
            0,
            4,
            0,
            _family);
        if (font == 0)
        {
            throw new InvalidOperationException(
                $"CreateFontW failed for {_family}.");
        }

        nint previous = SelectObject(_deviceContext, font);
        try
        {
            Mat2 transform = Mat2.Identity;
            uint required = GetGlyphOutline(
                _deviceContext,
                (uint)codePoint,
                Gray8Bitmap,
                out GlyphMetrics metrics,
                0,
                null,
                ref transform);
            if (required == GdiError)
            {
                throw new InvalidOperationException(
                    $"GetGlyphOutlineW failed for U+{codePoint:X4} in {_family}.");
            }

            byte[] native = new byte[required];
            fixed (byte* pointer = native)
            {
                uint written = GetGlyphOutline(
                    _deviceContext,
                    (uint)codePoint,
                    Gray8Bitmap,
                    out metrics,
                    required,
                    pointer,
                    ref transform);
                if (written == GdiError)
                {
                    throw new InvalidOperationException(
                        $"GetGlyphOutlineW bitmap failed for U+{codePoint:X4}.");
                }
            }

            int width = checked((int)metrics.BlackBoxX);
            int height = checked((int)metrics.BlackBoxY);
            int stride = height == 0
                ? 0
                : native.Length / height;
            int readableWidth = Math.Min(width, stride);
            byte[] alpha = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < readableWidth; x++)
                {
                    byte coverage = native[y * stride + x];
                    alpha[y * width + x] = (byte)Math.Min(
                        255,
                        coverage * 255 / 64);
                }
            }

            return new(
                width,
                height,
                metrics.GlyphOrigin.X,
                metrics.GlyphOrigin.Y,
                metrics.CellIncrementX,
                alpha);
        }
        finally
        {
            _ = SelectObject(_deviceContext, previous);
            _ = DeleteObject(font);
        }
    }

    [LibraryImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
    private static partial nint CreateCompatibleDC(nint deviceContext);

    [LibraryImport("gdi32.dll", EntryPoint = "DeleteDC")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint deviceContext);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW",
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateFont(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint characterSet,
        uint outputPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string faceName);

    [LibraryImport("gdi32.dll", EntryPoint = "SelectObject")]
    private static partial nint SelectObject(nint deviceContext, nint value);

    [LibraryImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint value);

    [LibraryImport("gdi32.dll", EntryPoint = "GetGlyphOutlineW")]
    private static partial uint GetGlyphOutline(
        nint deviceContext,
        uint character,
        uint format,
        out GlyphMetrics metrics,
        uint bufferSize,
        void* buffer,
        ref Mat2 transform);

    [StructLayout(LayoutKind.Sequential)]
    private struct GlyphMetrics
    {
        public uint BlackBoxX;
        public uint BlackBoxY;
        public Point GlyphOrigin;
        public int CellIncrementX;
        public int CellIncrementY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Fixed
    {
        public ushort Fraction;
        public short Value;

        public static Fixed One => new() { Value = 1 };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Mat2
    {
        public Fixed M11;
        public Fixed M12;
        public Fixed M21;
        public Fixed M22;

        public static Mat2 Identity => new()
        {
            M11 = Fixed.One,
            M22 = Fixed.One
        };
    }
}
