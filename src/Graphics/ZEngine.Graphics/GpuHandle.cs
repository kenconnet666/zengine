namespace ZEngine.Graphics;

/// <summary>
/// Identifies one generation of a GPU-owned resource slot. A recycled slot
/// never makes a previously issued handle valid again.
/// </summary>
public readonly record struct GpuHandle<TResource>(uint Index, uint Generation)
{
    public static GpuHandle<TResource> Null { get; } = new(uint.MaxValue, 0);

    public bool IsNull => Index == uint.MaxValue || Generation == 0;

    public override string ToString() =>
        IsNull ? $"{typeof(TResource).Name}:null" : $"{typeof(TResource).Name}:{Index}@{Generation}";
}
