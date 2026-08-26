namespace ZEngine.Assets;

public sealed record AssetResidency(
    string AssetId,
    ContentHash Content,
    int References,
    long Bytes);

public sealed class AssetStreamCache<TAsset>
{
    private readonly Dictionary<RevisionKey, ResidentRevision> _resident = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<AssetLease<TAsset>> AcquireAsync(
        AssetHandle<TAsset> handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        AssetArtifact artifact = handle.Current;
        RevisionKey key = new(handle.Id, artifact.Hash);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_resident.TryGetValue(key, out ResidentRevision? revision))
            {
                byte[] data = await File.ReadAllBytesAsync(
                    artifact.DataPath,
                    cancellationToken);
                revision = new(data);
                _resident.Add(key, revision);
            }

            revision.References++;
            return new(this, key, revision.Data);
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<AssetResidency> Snapshot()
    {
        _gate.Wait();
        try
        {
            return _resident.Select(pair => new AssetResidency(
                    pair.Key.Id.ToString(),
                    pair.Key.Hash,
                    pair.Value.References,
                    pair.Value.Data.LongLength))
                .OrderBy(item => item.AssetId, StringComparer.Ordinal)
                .ThenBy(item => item.Content.Value, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void Release(RevisionKey key)
    {
        _gate.Wait();
        try
        {
            if (!_resident.TryGetValue(key, out ResidentRevision? revision))
            {
                return;
            }

            if (--revision.References == 0)
            {
                _resident.Remove(key);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal readonly record struct RevisionKey(
        AssetId<TAsset> Id,
        ContentHash Hash);

    private sealed class ResidentRevision(byte[] data)
    {
        public byte[] Data { get; } = data;

        public int References { get; set; }
    }
}

public sealed class AssetLease<TAsset> : IDisposable
{
    private AssetStreamCache<TAsset>? _owner;
    private readonly AssetStreamCache<TAsset>.RevisionKey _key;

    internal AssetLease(
        AssetStreamCache<TAsset> owner,
        AssetStreamCache<TAsset>.RevisionKey key,
        byte[] data)
    {
        _owner = owner;
        _key = key;
        Data = data;
    }

    public ReadOnlyMemory<byte> Data { get; }

    public ContentHash Content => _key.Hash;

    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(_key);
}
