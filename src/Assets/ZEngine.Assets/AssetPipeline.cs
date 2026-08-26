using System.Security.Cryptography;
using System.Text;

namespace ZEngine.Assets;

public sealed class AssetPipeline
{
    private readonly AssetImporterRegistry _importers;
    private readonly ContentAddressedStore _store;

    public AssetPipeline(
        AssetImporterRegistry importers,
        ContentAddressedStore store)
    {
        _importers = importers;
        _store = store;
    }

    public async Task<AssetArtifact> BuildAsync(
        string sourcePath,
        string importerId,
        CancellationToken cancellationToken = default)
    {
        string fullSourcePath = Path.GetFullPath(sourcePath);
        IAssetImporter importer = _importers.Get(importerId);
        AssetImportContext context = new(fullSourcePath);
        AssetImportResult result = await importer.ImportAsync(
            context,
            cancellationToken);
        ArgumentNullException.ThrowIfNull(result.Data);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.MediaType);
        string[] dependencies = context.Dependencies
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ContentHash hash = await ComputeHashAsync(
            fullSourcePath,
            importer,
            dependencies,
            result.Data,
            cancellationToken);
        return await _store.PutAsync(
            hash,
            result.Data,
            new(
                fullSourcePath,
                importer.Id,
                importer.Version,
                result.MediaType,
                dependencies),
            cancellationToken);
    }

    private static async Task<ContentHash> ComputeHashAsync(
        string sourcePath,
        IAssetImporter importer,
        IReadOnlyList<string> dependencies,
        byte[] cooked,
        CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendText(importer.Id);
        AppendText(importer.Version);
        AppendText(Path.GetFileName(sourcePath));
        hash.AppendData(await File.ReadAllBytesAsync(sourcePath, cancellationToken));
        foreach (string dependency in dependencies)
        {
            AppendText(Path.GetFileName(dependency));
            hash.AppendData(await File.ReadAllBytesAsync(dependency, cancellationToken));
        }

        hash.AppendData(cooked);
        return new(Convert.ToHexString(hash.GetHashAndReset()));

        void AppendText(string value) =>
            hash.AppendData(Encoding.UTF8.GetBytes(value));
    }
}

public sealed class AssetMonitor<TAsset>
{
    private readonly AssetPipeline _pipeline;
    private readonly string _sourcePath;
    private readonly string _importerId;

    public AssetMonitor(
        AssetPipeline pipeline,
        AssetId<TAsset> id,
        string sourcePath,
        string importerId)
    {
        _pipeline = pipeline;
        _sourcePath = Path.GetFullPath(sourcePath);
        _importerId = importerId;
        Handle = new(id);
    }

    public AssetHandle<TAsset> Handle { get; }

    public async Task<bool> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        AssetArtifact artifact = await _pipeline.BuildAsync(
            _sourcePath,
            _importerId,
            cancellationToken);
        ulong before = Handle.Generation;
        Handle.Swap(artifact);
        return Handle.Generation != before;
    }
}
