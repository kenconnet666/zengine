namespace ZEngine.Plugins;

public sealed class HotState
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public int Count => _values.Count;

    public HotStateWriter Writer() => new(_values);

    public HotStateReader Reader() => new(_values);
}

public readonly struct HotStateWriter
{
    private readonly Dictionary<string, object> _values;

    internal HotStateWriter(Dictionary<string, object> values)
    {
        _values = values;
    }

    public void Write(string key, bool value) => WriteCore(key, value);

    public void Write(string key, int value) => WriteCore(key, value);

    public void Write(string key, long value) => WriteCore(key, value);

    public void Write(string key, double value) => WriteCore(key, value);

    public void Write(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteCore(key, value);
    }

    public void Write(string key, ReadOnlySpan<byte> value) =>
        WriteCore(key, value.ToArray());

    private void WriteCore(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _values[key] = value;
    }
}

public readonly struct HotStateReader
{
    private readonly IReadOnlyDictionary<string, object> _values;

    internal HotStateReader(IReadOnlyDictionary<string, object> values)
    {
        _values = values;
    }

    public T ReadOr<T>(string key, T fallback)
        where T : notnull =>
        _values.TryGetValue(key, out object? value) && value is T typed
            ? typed
            : fallback;
}
