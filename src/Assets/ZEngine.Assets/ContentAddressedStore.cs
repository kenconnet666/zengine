using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZEngine.Assets;

public sealed class ContentAddressedStore(string rootDirectory)
{
    private readonly string _rootDirectory = Path.GetFullPath(rootDirectory);

    public async Task<AssetArtifact> PutAsync(
        ContentHash hash,
        ReadOnlyMemory<byte> data,
        AssetArtifactManifest manifest,
        CancellationToken cancellationToken = default)
    {
        string directory = Path.Combine(_rootDirectory, hash.Value);
        Directory.CreateDirectory(directory);
        string dataPath = Path.Combine(directory, "asset.bin");
        string manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(dataPath))
        {
            await WriteAtomicAsync(dataPath, data, cancellationToken);
        }

        if (!File.Exists(manifestPath))
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(
                manifest,
                AssetJsonContext.Default.AssetArtifactManifest);
            await WriteAtomicAsync(manifestPath, json, cancellationToken);
        }

        return new(
            hash,
            dataPath,
            manifest.MediaType,
            data.Length,
            manifest.ImporterId,
            manifest.ImporterVersion,
            manifest.Dependencies);
    }

    private static async Task WriteAtomicAsync(
        string destination,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        string temporary = destination
                           + "."
                           + Guid.NewGuid().ToString("N")
                           + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, data, cancellationToken);
            File.Move(temporary, destination, overwrite: false);
        }
        catch (IOException) when (File.Exists(destination))
        {
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}

public sealed record AssetArtifactManifest(
    string SourcePath,
    string ImporterId,
    string ImporterVersion,
    string MediaType,
    IReadOnlyList<string> Dependencies);

[JsonSerializable(typeof(AssetArtifactManifest))]
internal partial class AssetJsonContext : JsonSerializerContext;
