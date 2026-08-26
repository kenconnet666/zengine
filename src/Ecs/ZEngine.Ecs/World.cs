using System.Runtime.InteropServices;

namespace ZEngine.Ecs;

public sealed class World
{
    private readonly Dictionary<ulong, Archetype> _archetypesByMask = [];
    private readonly List<Archetype> _archetypes = [];
    private readonly List<EntityLocation> _locations = [];
    private readonly Stack<int> _freeEntityIndices = [];
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;

    public int EntityCount { get; private set; }

    internal IReadOnlyList<Archetype> Archetypes => _archetypes;

    public Entity Spawn() => SpawnCore(0);

    public Entity Spawn<T1>(in T1 component1)
        where T1 : unmanaged
    {
        ComponentSchema schema1 = ComponentSchema<T1>.Value;
        Entity entity = SpawnCore(Bit(schema1));
        ref EntityLocation location = ref GetLocation(entity);
        location.Chunk!.GetColumn<T1>()[location.Row] = component1;
        return entity;
    }

    public Entity Spawn<T1, T2>(
        in T1 component1,
        in T2 component2)
        where T1 : unmanaged
        where T2 : unmanaged
    {
        ComponentSchema schema1 = ComponentSchema<T1>.Value;
        ComponentSchema schema2 = ComponentSchema<T2>.Value;
        EnsureDistinct(schema1, schema2);
        Entity entity = SpawnCore(Bit(schema1) | Bit(schema2));
        ref EntityLocation location = ref GetLocation(entity);
        location.Chunk!.GetColumn<T1>()[location.Row] = component1;
        location.Chunk.GetColumn<T2>()[location.Row] = component2;
        return entity;
    }

    public Entity Spawn<T1, T2, T3>(
        in T1 component1,
        in T2 component2,
        in T3 component3)
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        ComponentSchema schema1 = ComponentSchema<T1>.Value;
        ComponentSchema schema2 = ComponentSchema<T2>.Value;
        ComponentSchema schema3 = ComponentSchema<T3>.Value;
        EnsureDistinct(schema1, schema2);
        EnsureDistinct(schema1, schema3);
        EnsureDistinct(schema2, schema3);
        Entity entity = SpawnCore(
            Bit(schema1) | Bit(schema2) | Bit(schema3));
        ref EntityLocation location = ref GetLocation(entity);
        location.Chunk!.GetColumn<T1>()[location.Row] = component1;
        location.Chunk.GetColumn<T2>()[location.Row] = component2;
        location.Chunk.GetColumn<T3>()[location.Row] = component3;
        return entity;
    }

    public bool IsAlive(Entity entity)
    {
        if (entity.IsNull || (uint)entity.Index >= (uint)_locations.Count)
        {
            return false;
        }

        EntityLocation location = _locations[entity.Index];
        return location.Alive && location.Generation == entity.Generation;
    }

    public bool Has<T>(Entity entity)
        where T : unmanaged
    {
        ref EntityLocation location = ref GetLocation(entity);
        return (location.Archetype!.Mask & Bit(ComponentSchema<T>.Value)) != 0;
    }

    public ref T Get<T>(Entity entity)
        where T : unmanaged
    {
        ref EntityLocation location = ref GetLocation(entity);
        return ref location.Chunk!.GetColumn<T>()[location.Row];
    }

    public void Add<T>(Entity entity, in T component)
        where T : unmanaged
    {
        EnsureOwnerThread();
        ComponentSchema addedSchema = ComponentSchema<T>.Value;
        ref EntityLocation location = ref GetLocation(entity);
        if ((location.Archetype!.Mask & Bit(addedSchema)) != 0)
        {
            throw new InvalidOperationException(
                $"{entity} already has component {addedSchema.Name}.");
        }

        MoveEntity(
            entity,
            location.Archetype.Mask | Bit(addedSchema),
            addedSchema,
            component);
    }

    public void Remove<T>(Entity entity)
        where T : unmanaged
    {
        EnsureOwnerThread();
        ComponentSchema removedSchema = ComponentSchema<T>.Value;
        ref EntityLocation location = ref GetLocation(entity);
        if ((location.Archetype!.Mask & Bit(removedSchema)) == 0)
        {
            throw new InvalidOperationException(
                $"{entity} does not have component {removedSchema.Name}.");
        }

        MoveEntityWithoutValue(
            entity,
            location.Archetype.Mask & ~Bit(removedSchema));
    }

    public void Destroy(Entity entity)
    {
        EnsureOwnerThread();
        ref EntityLocation location = ref GetLocation(entity);
        Entity moved = location.Chunk!.RemoveSwapBack(location.Row);
        UpdateMovedEntity(moved, location.Row);
        location.Alive = false;
        location.Archetype = null;
        location.Chunk = null;
        location.Row = -1;
        location.Generation = NextGeneration(location.Generation);
        _freeEntityIndices.Push(entity.Index);
        EntityCount--;
    }

    public Query<TAccess1, TAccess2> Query<TAccess1, TAccess2>()
        where TAccess1 : struct, IComponentAccess
        where TAccess2 : struct, IComponentAccess
    {
        return new(this);
    }

    private Entity SpawnCore(ulong mask)
    {
        EnsureOwnerThread();
        int index;
        uint generation;
        if (_freeEntityIndices.TryPop(out index))
        {
            ref EntityLocation recycled = ref LocationAt(index);
            generation = recycled.Generation;
        }
        else
        {
            index = _locations.Count;
            generation = 1;
            _locations.Add(new() { Generation = generation, Row = -1 });
        }

        Entity entity = new(index, generation);
        Archetype archetype = GetOrCreateArchetype(mask);
        ArchetypeChunk chunk = archetype.AcquireChunk();
        int row = chunk.Add(entity);
        ref EntityLocation location = ref LocationAt(index);
        location.Generation = generation;
        location.Archetype = archetype;
        location.Chunk = chunk;
        location.Row = row;
        location.Alive = true;
        EntityCount++;
        return entity;
    }

    internal Entity SpawnSerialized(ulong mask) => SpawnCore(mask);

    internal void WriteSerializedComponent(
        Entity entity,
        ComponentSchema schema,
        ReadOnlySpan<byte> bytes)
    {
        ref EntityLocation location = ref GetLocation(entity);
        if (bytes.Length != schema.Size)
        {
            throw new InvalidDataException(
                $"Component {schema.Name} expected {schema.Size} bytes, got {bytes.Length}.");
        }

        SceneBinary.CopyToArray(
            bytes,
            location.Chunk!.GetColumn(schema),
            location.Row,
            schema.Size);
    }

    private void MoveEntity<T>(
        Entity entity,
        ulong destinationMask,
        ComponentSchema addedSchema,
        in T addedComponent)
        where T : unmanaged
    {
        MoveEntityCore(
            entity,
            destinationMask,
            static (ArchetypeChunk chunk, int row, in T value) =>
                chunk.GetColumn<T>()[row] = value,
            addedComponent,
            addedSchema);
    }

    private void MoveEntityWithoutValue(
        Entity entity,
        ulong destinationMask) =>
        MoveEntityCore<byte>(
            entity,
            destinationMask,
            static (ArchetypeChunk _, int _, in byte _) => { },
            0,
            addedSchema: null);

    private void MoveEntityCore<T>(
        Entity entity,
        ulong destinationMask,
        ComponentWriter<T> writer,
        in T value,
        ComponentSchema? addedSchema)
        where T : unmanaged
    {
        ref EntityLocation sourceLocation = ref GetLocation(entity);
        Archetype sourceArchetype = sourceLocation.Archetype!;
        ArchetypeChunk sourceChunk = sourceLocation.Chunk!;
        int sourceRow = sourceLocation.Row;
        Archetype destinationArchetype = GetOrCreateArchetype(destinationMask);
        ArchetypeChunk destinationChunk = destinationArchetype.AcquireChunk();
        int destinationRow = destinationChunk.Add(entity);

        foreach (ComponentSchema schema in sourceArchetype.Schemas)
        {
            if ((destinationMask & Bit(schema)) != 0)
            {
                Array.Copy(
                    sourceChunk.GetColumn(schema),
                    sourceRow,
                    destinationChunk.GetColumn(schema),
                    destinationRow,
                    1);
            }
        }

        if (addedSchema is not null)
        {
            writer(destinationChunk, destinationRow, value);
        }

        Entity moved = sourceChunk.RemoveSwapBack(sourceRow);
        UpdateMovedEntity(moved, sourceRow);
        sourceLocation.Archetype = destinationArchetype;
        sourceLocation.Chunk = destinationChunk;
        sourceLocation.Row = destinationRow;
    }

    private Archetype GetOrCreateArchetype(ulong mask)
    {
        if (_archetypesByMask.TryGetValue(mask, out Archetype? existing))
        {
            return existing;
        }

        List<ComponentSchema> schemas = [];
        for (int runtimeIndex = 0; runtimeIndex < 64; runtimeIndex++)
        {
            if ((mask & (1ul << runtimeIndex)) == 0)
            {
                continue;
            }

            ComponentSchema schema =
                ComponentRegistry.GetByRuntimeIndex(runtimeIndex);
            schemas.Add(schema);
        }

        Archetype created = new(mask, schemas.ToArray());
        _archetypesByMask.Add(mask, created);
        _archetypes.Add(created);
        return created;
    }

    private void UpdateMovedEntity(Entity moved, int row)
    {
        if (!moved.IsNull)
        {
            LocationAt(moved.Index).Row = row;
        }
    }

    private ref EntityLocation GetLocation(Entity entity)
    {
        if (!IsAlive(entity))
        {
            throw new InvalidOperationException($"{entity} is stale or not alive.");
        }

        return ref LocationAt(entity.Index);
    }

    private ref EntityLocation LocationAt(int index) =>
        ref CollectionsMarshal.AsSpan(_locations)[index];

    private void EnsureOwnerThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            throw new InvalidOperationException(
                "World structural access must occur on its owner thread.");
        }
    }

    private static ulong Bit(ComponentSchema schema) =>
        1ul << schema.RuntimeIndex;

    private static uint NextGeneration(uint generation)
    {
        uint next = generation + 1;
        return next == 0 ? 1 : next;
    }

    private static void EnsureDistinct(
        ComponentSchema left,
        ComponentSchema right)
    {
        if (left.RuntimeIndex == right.RuntimeIndex)
        {
            throw new ArgumentException(
                $"Component {left.Name} was provided more than once.");
        }
    }

    private delegate void ComponentWriter<T>(
        ArchetypeChunk chunk,
        int row,
        in T value)
        where T : unmanaged;

    private struct EntityLocation
    {
        public uint Generation;
        public Archetype? Archetype;
        public ArchetypeChunk? Chunk;
        public int Row;
        public bool Alive;
    }
}
