using System.Buffers.Binary;
using System.Text;

namespace ZEngine.UI.Windows;

public sealed record SystemFontFace(
    string Name,
    string Path,
    int FaceOffset)
{
    public bool Supports(int codePoint) =>
        TrueTypeCmap.Supports(Path, FaceOffset, codePoint);

    public bool Supports(string text)
    {
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (!Supports(rune.Value))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class SystemFontCatalog
{
    private static readonly string[] PreferredFiles =
    [
        "msyh.ttc",
        "msyhbd.ttc",
        "simsun.ttc",
        "Deng.ttf",
        "Dengb.ttf",
        "seguiemj.ttf",
        "segoeui.ttf"
    ];

    private readonly List<SystemFontFace> _faces;

    public SystemFontCatalog()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows system font catalog is only available on Windows.");
        }

        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        IEnumerable<string> files = PreferredFiles
            .Select(file => Path.Combine(fonts, file))
            .Where(File.Exists)
            .Concat(Directory.EnumerateFiles(fonts)
                .Where(path => Path.GetExtension(path) is ".ttf" or ".ttc" or ".otf"))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        _faces = files.SelectMany(ReadFaces).ToList();
    }

    public IReadOnlyList<SystemFontFace> Faces => _faces;

    public SystemFontFace Resolve(string text) =>
        _faces.FirstOrDefault(face => face.Supports(text))
        ?? throw new InvalidOperationException(
            $"No installed Windows font covers text '{text}'.");

    private static IEnumerable<SystemFontFace> ReadFaces(string path)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            yield break;
        }

        if (data.Length < 12)
        {
            yield break;
        }

        if (data.AsSpan(0, 4).SequenceEqual("ttcf"u8))
        {
            int count = checked((int)ReadUInt32(data, 8));
            for (int index = 0; index < count; index++)
            {
                int offset = checked((int)ReadUInt32(data, 12 + index * 4));
                yield return new(
                    $"{Path.GetFileNameWithoutExtension(path)}#{index}",
                    path,
                    offset);
            }
        }
        else
        {
            yield return new(
                Path.GetFileNameWithoutExtension(path),
                path,
                0);
        }
    }

    private static uint ReadUInt32(byte[] data, int offset) =>
        offset >= 0 && offset + 4 <= data.Length
            ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4))
            : 0;
}

internal static class TrueTypeCmap
{
    public static bool Supports(
        string path,
        int faceOffset,
        int codePoint)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return false;
        }

        int cmap = FindTable(data, faceOffset, "cmap"u8);
        if (cmap <= 0 || cmap + 4 > data.Length)
        {
            return false;
        }

        int tableCount = ReadUInt16(data, cmap + 2);
        for (int index = 0; index < tableCount; index++)
        {
            int record = cmap + 4 + index * 8;
            int subtable = cmap + checked((int)ReadUInt32(data, record + 4));
            int format = ReadUInt16(data, subtable);
            if (format == 12 && SupportsFormat12(data, subtable, codePoint))
            {
                return true;
            }

            if (format == 4
                && codePoint <= ushort.MaxValue
                && SupportsFormat4(data, subtable, codePoint))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindTable(
        byte[] data,
        int faceOffset,
        ReadOnlySpan<byte> tag)
    {
        int tableCount = ReadUInt16(data, faceOffset + 4);
        for (int index = 0; index < tableCount; index++)
        {
            int record = faceOffset + 12 + index * 16;
            if (record + 16 <= data.Length
                && data.AsSpan(record, 4).SequenceEqual(tag))
            {
                return checked((int)ReadUInt32(data, record + 8));
            }
        }

        return -1;
    }

    private static bool SupportsFormat12(
        byte[] data,
        int offset,
        int codePoint)
    {
        int groups = checked((int)ReadUInt32(data, offset + 12));
        int low = 0;
        int high = groups - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            int group = offset + 16 + middle * 12;
            uint start = ReadUInt32(data, group);
            uint end = ReadUInt32(data, group + 4);
            if ((uint)codePoint < start)
            {
                high = middle - 1;
            }
            else if ((uint)codePoint > end)
            {
                low = middle + 1;
            }
            else
            {
                uint glyph = ReadUInt32(data, group + 8)
                             + (uint)codePoint - start;
                return glyph != 0;
            }
        }

        return false;
    }

    private static bool SupportsFormat4(
        byte[] data,
        int offset,
        int codePoint)
    {
        int segmentCount = ReadUInt16(data, offset + 6) / 2;
        int endCodes = offset + 14;
        int startCodes = endCodes + segmentCount * 2 + 2;
        int deltas = startCodes + segmentCount * 2;
        int rangeOffsets = deltas + segmentCount * 2;
        for (int segment = 0; segment < segmentCount; segment++)
        {
            int end = ReadUInt16(data, endCodes + segment * 2);
            if (codePoint > end)
            {
                continue;
            }

            int start = ReadUInt16(data, startCodes + segment * 2);
            if (codePoint < start)
            {
                return false;
            }

            short delta = unchecked((short)ReadUInt16(
                data,
                deltas + segment * 2));
            int rangeOffsetAddress = rangeOffsets + segment * 2;
            int rangeOffset = ReadUInt16(data, rangeOffsetAddress);
            if (rangeOffset == 0)
            {
                return ((codePoint + delta) & 0xFFFF) != 0;
            }

            int glyphAddress = rangeOffsetAddress
                               + rangeOffset
                               + (codePoint - start) * 2;
            int glyph = ReadUInt16(data, glyphAddress);
            return glyph != 0 && ((glyph + delta) & 0xFFFF) != 0;
        }

        return false;
    }

    private static int ReadUInt16(byte[] data, int offset) =>
        offset >= 0 && offset + 2 <= data.Length
            ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2))
            : 0;

    private static uint ReadUInt32(byte[] data, int offset) =>
        offset >= 0 && offset + 4 <= data.Length
            ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4))
            : 0;
}
