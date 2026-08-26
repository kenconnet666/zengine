namespace ZEngine.Ecs;

public sealed record ArchetypeSnapshot(
    ulong Mask,
    IReadOnlyList<string> Components,
    int EntityCount,
    int ChunkCount,
    int Capacity);

public sealed record WorldSnapshot(
    int EntityCount,
    IReadOnlyList<ArchetypeSnapshot> Archetypes);

public sealed partial class World
{
    public WorldSnapshot Inspect() => new(
        EntityCount,
        _archetypes
            .OrderBy(archetype => archetype.Mask)
            .Select(archetype => new ArchetypeSnapshot(
                archetype.Mask,
                archetype.Schemas
                    .Select(schema => schema.Name)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                archetype.Chunks.Sum(chunk => chunk.Count),
                archetype.Chunks.Count,
                archetype.Chunks.Count * Archetype.ChunkCapacity))
            .ToArray());
}
