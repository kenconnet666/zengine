using System.Runtime.InteropServices;

namespace ZEngine.Ecs;

public static class SceneSerializer
{
    private const uint Magic = 0x4E43535A;
    private const ushort Version = 1;

    public static void Save(World world, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(destination);
        using BinaryWriter writer = new(
            destination,
            System.Text.Encoding.UTF8,
            leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        Archetype[] archetypes = world.Archetypes
            .Where(archetype => archetype.Chunks.Any(chunk => chunk.Count > 0))
            .ToArray();
        writer.Write(archetypes.Length);
        foreach (Archetype archetype in archetypes)
        {
            writer.Write(archetype.Schemas.Length);
            foreach (ComponentSchema schema in archetype.Schemas)
            {
                writer.Write(schema.StableId);
                writer.Write(schema.Size);
            }

            int entityCount = archetype.Chunks.Sum(chunk => chunk.Count);
            writer.Write(entityCount);
            foreach (ArchetypeChunk chunk in archetype.Chunks)
            {
                for (int row = 0; row < chunk.Count; row++)
                {
                    foreach (ComponentSchema schema in archetype.Schemas)
                    {
                        SceneBinary.WriteArrayElement(
                            writer,
                            chunk.GetColumn(schema),
                            row,
                            schema.Size);
                    }
                }
            }
        }
    }

    public static World Load(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using BinaryReader reader = new(
            source,
            System.Text.Encoding.UTF8,
            leaveOpen: true);
        if (reader.ReadUInt32() != Magic)
        {
            throw new InvalidDataException("Scene magic does not match ZSCN.");
        }

        if (reader.ReadUInt16() != Version)
        {
            throw new InvalidDataException("Scene version is not supported.");
        }

        World world = new();
        int archetypeCount = reader.ReadInt32();
        for (int archetypeIndex = 0; archetypeIndex < archetypeCount; archetypeIndex++)
        {
            int schemaCount = reader.ReadInt32();
            ComponentSchema[] schemas = new ComponentSchema[schemaCount];
            ulong mask = 0;
            for (int schemaIndex = 0; schemaIndex < schemaCount; schemaIndex++)
            {
                ComponentSchema schema = ComponentRegistry.GetByStableId(
                    reader.ReadUInt64());
                int serializedSize = reader.ReadInt32();
                if (serializedSize != schema.Size)
                {
                    throw new InvalidDataException(
                        $"Component {schema.Name} size changed from {serializedSize} to {schema.Size}.");
                }

                schemas[schemaIndex] = schema;
                mask |= 1ul << schema.RuntimeIndex;
            }

            int entityCount = reader.ReadInt32();
            for (int entityIndex = 0; entityIndex < entityCount; entityIndex++)
            {
                Entity entity = world.SpawnSerialized(mask);
                foreach (ComponentSchema schema in schemas)
                {
                    byte[] bytes = reader.ReadBytes(schema.Size);
                    if (bytes.Length != schema.Size)
                    {
                        throw new EndOfStreamException(
                            $"Scene ended while reading {schema.Name}.");
                    }

                    world.WriteSerializedComponent(entity, schema, bytes);
                }
            }
        }

        return world;
    }
}

internal static unsafe class SceneBinary
{
    public static void WriteArrayElement(
        BinaryWriter writer,
        Array column,
        int row,
        int size)
    {
        GCHandle handle = GCHandle.Alloc(column, GCHandleType.Pinned);
        try
        {
            byte* pointer = (byte*)handle.AddrOfPinnedObject() + row * size;
            writer.Write(new ReadOnlySpan<byte>(pointer, size));
        }
        finally
        {
            handle.Free();
        }
    }

    public static void CopyToArray(
        ReadOnlySpan<byte> source,
        Array destination,
        int row,
        int size)
    {
        GCHandle handle = GCHandle.Alloc(destination, GCHandleType.Pinned);
        try
        {
            byte* pointer = (byte*)handle.AddrOfPinnedObject() + row * size;
            source.CopyTo(new Span<byte>(pointer, size));
        }
        finally
        {
            handle.Free();
        }
    }
}
