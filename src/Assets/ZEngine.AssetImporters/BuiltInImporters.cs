using System.Buffers.Binary;
using System.Text.Json;
using ZEngine.Assets;

namespace ZEngine.AssetImporters;

public sealed class SpirvShaderImporter : IAssetImporter
{
    private const uint SpirvMagic = 0x07230203;

    public string Id => "zengine.shader.spirv";

    public string Version => "1.0.0";

    public async ValueTask<AssetImportResult> ImportAsync(
        AssetImportContext context,
        CancellationToken cancellationToken)
    {
        byte[] source = await context.ReadSourceAsync(cancellationToken);
        if (source.Length < 20 || source.Length % sizeof(uint) != 0
            || BinaryPrimitives.ReadUInt32LittleEndian(source) != SpirvMagic)
        {
            throw new InvalidDataException(
                $"'{context.SourcePath}' is not a valid little-endian SPIR-V module.");
        }

        return new(source, "application/vnd.khronos.spirv");
    }
}

public sealed class PngTextureImporter : IAssetImporter
{
    private static ReadOnlySpan<byte> Signature =>
        [137, 80, 78, 71, 13, 10, 26, 10];

    public string Id => "zengine.texture.png";

    public string Version => "1.0.0";

    public async ValueTask<AssetImportResult> ImportAsync(
        AssetImportContext context,
        CancellationToken cancellationToken)
    {
        byte[] source = await context.ReadSourceAsync(cancellationToken);
        if (source.Length < 24 || !source.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new InvalidDataException(
                $"'{context.SourcePath}' is not a PNG image.");
        }

        uint width = BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(16, 4));
        uint height = BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(20, 4));
        if (width == 0 || height == 0)
        {
            throw new InvalidDataException("PNG dimensions must be non-zero.");
        }

        return new(source, "image/png");
    }
}

public sealed class JsonSceneImporter : IAssetImporter
{
    public string Id => "zengine.scene.json";

    public string Version => "1.0.0";

    public async ValueTask<AssetImportResult> ImportAsync(
        AssetImportContext context,
        CancellationToken cancellationToken)
    {
        byte[] source = await context.ReadSourceAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(source);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A scene source must be a JSON object.");
        }

        using MemoryStream cooked = new();
        await JsonSerializer.SerializeAsync(
            cooked,
            document.RootElement,
            cancellationToken: cancellationToken);
        return new(cooked.ToArray(), "application/vnd.zengine.scene+json");
    }
}
