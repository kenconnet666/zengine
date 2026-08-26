namespace ZEngine.Assets;

public interface IAssetImporter
{
    string Id { get; }

    string Version { get; }

    ValueTask<AssetImportResult> ImportAsync(
        AssetImportContext context,
        CancellationToken cancellationToken);
}

public sealed record AssetImportResult(
    byte[] Data,
    string MediaType);

public sealed class AssetImportContext
{
    private readonly HashSet<string> _dependencies = new(
        StringComparer.OrdinalIgnoreCase);

    internal AssetImportContext(string sourcePath)
    {
        SourcePath = Path.GetFullPath(sourcePath);
    }

    public string SourcePath { get; }

    public IReadOnlySet<string> Dependencies => _dependencies;

    public async ValueTask<byte[]> ReadSourceAsync(
        CancellationToken cancellationToken = default) =>
        await File.ReadAllBytesAsync(SourcePath, cancellationToken);

    public string AddDependency(string path)
    {
        string fullPath = Path.GetFullPath(path, Path.GetDirectoryName(SourcePath)!);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Asset importer dependency does not exist.",
                fullPath);
        }

        _dependencies.Add(fullPath);
        return fullPath;
    }
}

public sealed class AssetImporterRegistry
{
    private readonly Dictionary<string, IAssetImporter> _importers = new(
        StringComparer.Ordinal);
    private readonly object _gate = new();

    public IDisposable Register(IAssetImporter importer)
    {
        ArgumentNullException.ThrowIfNull(importer);
        lock (_gate)
        {
            if (!_importers.TryAdd(importer.Id, importer))
            {
                throw new InvalidOperationException(
                    $"Asset importer '{importer.Id}' is already registered.");
            }
        }

        return new Registration(this, importer);
    }

    public IAssetImporter Get(string id)
    {
        lock (_gate)
        {
            return _importers.TryGetValue(id, out IAssetImporter? importer)
                ? importer
                : throw new KeyNotFoundException(
                    $"Asset importer '{id}' is not registered.");
        }
    }

    private void Remove(IAssetImporter importer)
    {
        lock (_gate)
        {
            if (_importers.TryGetValue(importer.Id, out IAssetImporter? current)
                && ReferenceEquals(current, importer))
            {
                _importers.Remove(importer.Id);
            }
        }
    }

    private sealed class Registration(
        AssetImporterRegistry registry,
        IAssetImporter importer) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                registry.Remove(importer);
            }
        }
    }
}
