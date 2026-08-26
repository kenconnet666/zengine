namespace ZEngine.Ecs;

public sealed class RenderWorldSnapshot<TFirst, TSecond>
    where TFirst : unmanaged
    where TSecond : unmanaged
{
    private TFirst[] _first = [];
    private TSecond[] _second = [];

    public int Count { get; private set; }

    public ulong Generation { get; private set; }

    public ReadOnlySpan<TFirst> First => _first.AsSpan(0, Count);

    public ReadOnlySpan<TSecond> Second => _second.AsSpan(0, Count);

    internal void Extract(World world, ulong generation)
    {
        Query<Read<TFirst>, Read<TSecond>> query =
            world.Query<Read<TFirst>, Read<TSecond>>();
        int writeOffset = 0;
        foreach (QueryChunk chunk in query.Chunks)
        {
            EnsureCapacity(writeOffset + chunk.Count);
            chunk.Read<TFirst>().CopyTo(
                _first.AsSpan(writeOffset, chunk.Count));
            chunk.Read<TSecond>().CopyTo(
                _second.AsSpan(writeOffset, chunk.Count));
            writeOffset += chunk.Count;
        }

        Count = writeOffset;
        Generation = generation;
    }

    private void EnsureCapacity(int required)
    {
        if (_first.Length >= required)
        {
            return;
        }

        int capacity = Math.Max(required, Math.Max(256, _first.Length * 2));
        Array.Resize(ref _first, capacity);
        Array.Resize(ref _second, capacity);
    }
}

public sealed class RenderWorldBuffer<TFirst, TSecond>
    where TFirst : unmanaged
    where TSecond : unmanaged
{
    private readonly RenderWorldSnapshot<TFirst, TSecond>[] _snapshots =
        [new(), new()];
    private int _currentIndex;
    private ulong _generation;

    public RenderWorldSnapshot<TFirst, TSecond> Current =>
        _snapshots[_currentIndex];

    public RenderWorldSnapshot<TFirst, TSecond> Extract(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        int nextIndex = 1 - _currentIndex;
        RenderWorldSnapshot<TFirst, TSecond> next = _snapshots[nextIndex];
        next.Extract(world, ++_generation);
        Volatile.Write(ref _currentIndex, nextIndex);
        return next;
    }
}
