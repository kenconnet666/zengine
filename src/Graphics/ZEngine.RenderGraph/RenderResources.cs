using ZEngine.Graphics;

namespace ZEngine.RenderGraph;

public readonly struct ColorTarget;

public readonly struct DepthTarget;

public readonly struct SampledTexture;

public readonly struct StorageTexture;

public readonly struct VertexData;

public readonly struct IndexData;

public readonly struct UniformData;

public readonly struct StorageData;

public readonly record struct RenderImage<TKind>
{
    private readonly RenderResourceHandle _handle;

    internal RenderImage(RenderResourceHandle handle)
    {
        _handle = handle;
    }

    public bool IsNull => _handle.IsNull;

    internal RenderResourceHandle Handle => _handle;

    public override string ToString() =>
        _handle.Format(typeof(TKind).Name);
}

public readonly record struct RenderBuffer<TKind>
{
    private readonly RenderResourceHandle _handle;

    internal RenderBuffer(RenderResourceHandle handle)
    {
        _handle = handle;
    }

    public bool IsNull => _handle.IsNull;

    internal RenderResourceHandle Handle => _handle;

    public override string ToString() =>
        _handle.Format(typeof(TKind).Name);
}

internal enum RenderResourceKind
{
    Image,
    Buffer
}

internal readonly record struct RenderResourceHandle(
    long GraphId,
    int Index,
    RenderResourceKind Kind)
{
    public bool IsNull => GraphId == 0 || Index < 0;

    public string Format(string marker) =>
        IsNull ? $"{marker}:null" : $"{marker}:{Index}@graph-{GraphId}";
}

internal sealed record RenderResourceDefinition(
    int Index,
    string Name,
    RenderResourceKind Kind,
    object Descriptor,
    bool Imported);
