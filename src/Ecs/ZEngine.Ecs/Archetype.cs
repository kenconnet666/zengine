namespace ZEngine.Ecs;

internal sealed class Archetype
{
    public const int ChunkCapacity = 1024;
    private readonly List<ArchetypeChunk> _chunks = [];

    public Archetype(ulong mask, ComponentSchema[] schemas)
    {
        Mask = mask;
        Schemas = schemas;
    }

    public ulong Mask { get; }

    public ComponentSchema[] Schemas { get; }

    public IReadOnlyList<ArchetypeChunk> Chunks => _chunks;

    public ArchetypeChunk AcquireChunk()
    {
        if (_chunks.Count > 0 && _chunks[^1].Count < ChunkCapacity)
        {
            return _chunks[^1];
        }

        ArchetypeChunk chunk = new(this);
        _chunks.Add(chunk);
        return chunk;
    }
}

internal sealed class ArchetypeChunk
{
    private readonly Entity[] _entities = new Entity[Archetype.ChunkCapacity];
    private readonly Array?[] _columns = new Array[64];

    public ArchetypeChunk(Archetype archetype)
    {
        Archetype = archetype;
        foreach (ComponentSchema schema in archetype.Schemas)
        {
            _columns[schema.RuntimeIndex] = Array.CreateInstance(
                schema.Type,
                Archetype.ChunkCapacity);
        }
    }

    public Archetype Archetype { get; }

    public int Count { get; private set; }

    public ReadOnlySpan<Entity> Entities => _entities.AsSpan(0, Count);

    public int Add(Entity entity)
    {
        if (Count == Archetype.ChunkCapacity)
        {
            throw new InvalidOperationException("The archetype chunk is full.");
        }

        int row = Count++;
        _entities[row] = entity;
        return row;
    }

    public Entity RemoveSwapBack(int row)
    {
        int last = --Count;
        Entity moved = Entity.Null;
        if (row != last)
        {
            moved = _entities[last];
            _entities[row] = moved;
            foreach (ComponentSchema schema in Archetype.Schemas)
            {
                Array column = _columns[schema.RuntimeIndex]!;
                Array.Copy(column, last, column, row, 1);
            }
        }

        _entities[last] = Entity.Null;
        foreach (ComponentSchema schema in Archetype.Schemas)
        {
            Array.Clear(_columns[schema.RuntimeIndex]!, last, 1);
        }

        return moved;
    }

    public T[] GetColumn<T>()
        where T : unmanaged
    {
        ComponentSchema schema = ComponentSchema<T>.Value;
        return _columns[schema.RuntimeIndex] as T[]
            ?? throw new InvalidOperationException(
                $"Archetype 0x{Archetype.Mask:X16} has no {schema.Name} column.");
    }

    public Array GetColumn(ComponentSchema schema) =>
        _columns[schema.RuntimeIndex]
        ?? throw new InvalidOperationException(
            $"Archetype 0x{Archetype.Mask:X16} has no {schema.Name} column.");
}
