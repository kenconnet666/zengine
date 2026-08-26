namespace ZEngine.Assets;

public readonly record struct ContentHash
{
    public ContentHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new FormatException(
                "Content hash must be a 64-character SHA-256 hexadecimal string.");
        }

        Value = value.ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct AssetId<TAsset>(Guid Value)
{
    public static AssetId<TAsset> FromName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        byte[] digest = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(name));
        return new(new Guid(digest.AsSpan(0, 16), bigEndian: true));
    }

    public override string ToString() => $"{typeof(TAsset).Name}:{Value:N}";
}

public sealed record AssetArtifact(
    ContentHash Hash,
    string DataPath,
    string MediaType,
    long ByteLength,
    string ImporterId,
    string ImporterVersion,
    IReadOnlyList<string> Dependencies);

public sealed class AssetHandle<TAsset>
{
    private AssetArtifact? _current;
    private long _generation;

    public AssetHandle(AssetId<TAsset> id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Asset id cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public AssetId<TAsset> Id { get; }

    public ulong Generation => unchecked((ulong)Volatile.Read(ref _generation));

    public AssetArtifact Current => Volatile.Read(ref _current)
        ?? throw new InvalidOperationException("Asset handle has no active artifact.");

    public void Swap(AssetArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        AssetArtifact? previous = Interlocked.Exchange(ref _current, artifact);
        if (previous?.Hash != artifact.Hash)
        {
            Interlocked.Increment(ref _generation);
        }
    }
}
