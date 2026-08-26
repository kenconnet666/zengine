namespace ZEngine.Ecs;

public enum ComponentAccessMode
{
    Read,
    Write
}

public interface IComponentAccess
{
    static abstract ComponentSchema Schema { get; }

    static abstract ComponentAccessMode Mode { get; }
}

public readonly struct Read<T> : IComponentAccess
    where T : unmanaged
{
    public static ComponentSchema Schema => ComponentSchema<T>.Value;

    public static ComponentAccessMode Mode => ComponentAccessMode.Read;
}

public readonly struct Write<T> : IComponentAccess
    where T : unmanaged
{
    public static ComponentSchema Schema => ComponentSchema<T>.Value;

    public static ComponentAccessMode Mode => ComponentAccessMode.Write;
}

public readonly ref struct Query<TAccess1, TAccess2>
    where TAccess1 : struct, IComponentAccess
    where TAccess2 : struct, IComponentAccess
{
    private readonly World _world;
    private readonly ulong _requiredMask;
    private readonly ulong _readMask;
    private readonly ulong _writeMask;

    internal Query(World world)
    {
        ComponentSchema first = TAccess1.Schema;
        ComponentSchema second = TAccess2.Schema;
        if (first.RuntimeIndex == second.RuntimeIndex)
        {
            throw new ArgumentException(
                $"Query declares component {first.Name} more than once.");
        }

        _world = world;
        ulong firstBit = 1ul << first.RuntimeIndex;
        ulong secondBit = 1ul << second.RuntimeIndex;
        _requiredMask = firstBit | secondBit;
        _readMask =
            (TAccess1.Mode == ComponentAccessMode.Read ? firstBit : 0)
            | (TAccess2.Mode == ComponentAccessMode.Read ? secondBit : 0);
        _writeMask =
            (TAccess1.Mode == ComponentAccessMode.Write ? firstBit : 0)
            | (TAccess2.Mode == ComponentAccessMode.Write ? secondBit : 0);
    }

    public QueryChunkEnumerable Chunks =>
        new(_world, _requiredMask, _readMask, _writeMask);
}

public readonly ref struct QueryChunkEnumerable
{
    private readonly World _world;
    private readonly ulong _requiredMask;
    private readonly ulong _readMask;
    private readonly ulong _writeMask;

    internal QueryChunkEnumerable(
        World world,
        ulong requiredMask,
        ulong readMask,
        ulong writeMask)
    {
        _world = world;
        _requiredMask = requiredMask;
        _readMask = readMask;
        _writeMask = writeMask;
    }

    public Enumerator GetEnumerator() =>
        new(_world, _requiredMask, _readMask, _writeMask);

    public ref struct Enumerator
    {
        private readonly World _world;
        private readonly ulong _requiredMask;
        private readonly ulong _readMask;
        private readonly ulong _writeMask;
        private int _archetypeIndex;
        private int _chunkIndex;
        private ArchetypeChunk? _current;

        internal Enumerator(
            World world,
            ulong requiredMask,
            ulong readMask,
            ulong writeMask)
        {
            _world = world;
            _requiredMask = requiredMask;
            _readMask = readMask;
            _writeMask = writeMask;
            _archetypeIndex = 0;
            _chunkIndex = 0;
            _current = null;
        }

        public QueryChunk Current =>
            new(
                _current
                ?? throw new InvalidOperationException(
                    "The query enumerator is not positioned on a chunk."),
                _readMask,
                _writeMask);

        public bool MoveNext()
        {
            IReadOnlyList<Archetype> archetypes = _world.Archetypes;
            while (_archetypeIndex < archetypes.Count)
            {
                Archetype archetype = archetypes[_archetypeIndex];
                if ((archetype.Mask & _requiredMask) != _requiredMask)
                {
                    _archetypeIndex++;
                    _chunkIndex = 0;
                    continue;
                }

                IReadOnlyList<ArchetypeChunk> chunks = archetype.Chunks;
                while (_chunkIndex < chunks.Count)
                {
                    ArchetypeChunk chunk = chunks[_chunkIndex++];
                    if (chunk.Count > 0)
                    {
                        _current = chunk;
                        return true;
                    }
                }

                _archetypeIndex++;
                _chunkIndex = 0;
            }

            _current = null;
            return false;
        }
    }
}

public readonly ref struct QueryChunk
{
    private readonly ArchetypeChunk _chunk;
    private readonly ulong _readMask;
    private readonly ulong _writeMask;

    internal QueryChunk(
        ArchetypeChunk chunk,
        ulong readMask,
        ulong writeMask)
    {
        _chunk = chunk;
        _readMask = readMask;
        _writeMask = writeMask;
    }

    public int Count => _chunk.Count;

    public ReadOnlySpan<T> Read<T>()
        where T : unmanaged
    {
        ComponentSchema schema = ComponentSchema<T>.Value;
        ulong bit = 1ul << schema.RuntimeIndex;
        if (((_readMask | _writeMask) & bit) == 0)
        {
            throw new InvalidOperationException(
                $"Query did not declare read access to {schema.Name}.");
        }

        return _chunk.GetColumn<T>().AsSpan(0, _chunk.Count);
    }

    public Span<T> Write<T>()
        where T : unmanaged
    {
        ComponentSchema schema = ComponentSchema<T>.Value;
        ulong bit = 1ul << schema.RuntimeIndex;
        if ((_writeMask & bit) == 0)
        {
            throw new InvalidOperationException(
                $"Query did not declare write access to {schema.Name}.");
        }

        return _chunk.GetColumn<T>().AsSpan(0, _chunk.Count);
    }
}
