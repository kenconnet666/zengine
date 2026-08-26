using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace ZEngine.Ecs;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class ComponentAttribute : Attribute;

public sealed record ComponentSchema(
    ulong StableId,
    int RuntimeIndex,
    string Name,
    Type Type,
    int Size);

public static class ComponentSchema<T>
    where T : unmanaged
{
    public static ComponentSchema Value { get; } =
        ComponentRegistry.Register<T>();
}

internal static class ComponentRegistry
{
    private static readonly ConcurrentDictionary<Type, ComponentSchema> Schemas = new();
    private static readonly ComponentSchema?[] ByRuntimeIndex = new ComponentSchema[64];
    private static readonly ConcurrentDictionary<ulong, ComponentSchema> ByStableId = new();
    private static int _nextRuntimeIndex = -1;

    public static ComponentSchema Register<T>()
        where T : unmanaged
    {
        ComponentSchema schema = Schemas.GetOrAdd(
            typeof(T),
            static type =>
            {
                int runtimeIndex = Interlocked.Increment(ref _nextRuntimeIndex);
                if (runtimeIndex >= 64)
                {
                    throw new NotSupportedException(
                        "The P2 ECS profile supports at most 64 unmanaged component types per process.");
                }

                string name = type.FullName ?? type.Name;
                return new(
                    StableHash(name),
                    runtimeIndex,
                    name,
                    type,
                    Unsafe.SizeOf<T>());
            });
        Volatile.Write(ref ByRuntimeIndex[schema.RuntimeIndex], schema);
        if (!ByStableId.TryAdd(schema.StableId, schema)
            && ByStableId[schema.StableId].Type != schema.Type)
        {
            throw new InvalidOperationException(
                $"Component schema hash collision for 0x{schema.StableId:X16}.");
        }

        return schema;
    }

    public static ComponentSchema GetByRuntimeIndex(int runtimeIndex) =>
        Volatile.Read(ref ByRuntimeIndex[runtimeIndex])
        ?? throw new InvalidOperationException(
            $"No component schema is registered for runtime index {runtimeIndex}.");

    public static ComponentSchema GetByStableId(ulong stableId) =>
        ByStableId.TryGetValue(stableId, out ComponentSchema? schema)
            ? schema
            : throw new InvalidDataException(
                $"Scene requires unregistered component schema 0x{stableId:X16}.");

    private static ulong StableHash(string value)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        ulong hash = offset;
        foreach (byte valueByte in Encoding.UTF8.GetBytes(value))
        {
            hash ^= valueByte;
            hash *= prime;
        }

        return hash;
    }
}
