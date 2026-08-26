namespace ZEngine.Editor;

public enum EditorLogLevel
{
    Trace,
    Information,
    Warning,
    Error
}

public sealed record EditorLogEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    EditorLogLevel Level,
    string Source,
    string Message);

public sealed class EditorConsole(int capacity = 1024)
{
    private readonly Queue<EditorLogEntry> _entries = new(capacity);
    private readonly object _gate = new();
    private long _sequence;

    public void Write(
        EditorLogLevel level,
        string source,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        lock (_gate)
        {
            while (_entries.Count >= capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(new(
                ++_sequence,
                DateTimeOffset.UtcNow,
                level,
                source,
                message));
        }
    }

    public IReadOnlyList<EditorLogEntry> Snapshot(int maximum = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        lock (_gate)
        {
            return _entries.TakeLast(maximum).ToArray();
        }
    }
}
