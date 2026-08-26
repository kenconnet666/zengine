namespace ZEngine.RenderGraph;

public delegate void RenderPassSetup<TData>(RenderPassBuilder<TData> pass)
    where TData : struct;

public delegate void RenderPassExecute<TData>(
    ref RenderGraphCommandContext commands,
    in TData data)
    where TData : struct;

public sealed class RenderPassBuilder<TData>
    where TData : struct
{
    private readonly RenderGraphBuilder _graph;
    private readonly RenderPassDefinition _definition;
    private TData _data;
    private RenderPassExecute<TData>? _execute;

    internal RenderPassBuilder(
        RenderGraphBuilder graph,
        RenderPassDefinition definition)
    {
        _graph = graph;
        _definition = definition;
    }

    public ref TData Data => ref _data;

    public RenderImage<TKind> Read<TKind>(RenderImage<TKind> image) =>
        Read(image, ImageAccess.Sampled());

    public RenderImage<TKind> Read<TKind>(
        RenderImage<TKind> image,
        ImageAccess access)
    {
        _graph.RegisterAccess(
            _definition,
            image.Handle,
            RenderAccessMode.Read,
            access);
        return image;
    }

    public RenderImage<TKind> Write<TKind>(
        RenderImage<TKind> image,
        ImageAccess access)
    {
        _graph.RegisterAccess(
            _definition,
            image.Handle,
            RenderAccessMode.Write,
            access);
        return image;
    }

    public RenderImage<TKind> ReadWrite<TKind>(
        RenderImage<TKind> image,
        ImageAccess access)
    {
        _graph.RegisterAccess(
            _definition,
            image.Handle,
            RenderAccessMode.ReadWrite,
            access);
        return image;
    }

    public RenderBuffer<TKind> Read<TKind>(
        RenderBuffer<TKind> buffer,
        BufferAccess access)
    {
        _graph.RegisterAccess(
            _definition,
            buffer.Handle,
            RenderAccessMode.Read,
            access);
        return buffer;
    }

    public RenderBuffer<TKind> Write<TKind>(
        RenderBuffer<TKind> buffer,
        BufferAccess access)
    {
        _graph.RegisterAccess(
            _definition,
            buffer.Handle,
            RenderAccessMode.Write,
            access);
        return buffer;
    }

    public RenderBuffer<TKind> ReadWrite<TKind>(
        RenderBuffer<TKind> buffer,
        BufferAccess access)
    {
        _graph.RegisterAccess(
            _definition,
            buffer.Handle,
            RenderAccessMode.ReadWrite,
            access);
        return buffer;
    }

    public RenderPassBuilder<TData> After(params ReadOnlySpan<string> passNames)
    {
        foreach (string passName in passNames)
        {
            _definition.After.Add(ValidatePassName(passName));
        }

        return this;
    }

    public RenderPassBuilder<TData> Before(params ReadOnlySpan<string> passNames)
    {
        foreach (string passName in passNames)
        {
            _definition.Before.Add(ValidatePassName(passName));
        }

        return this;
    }

    public RenderPassBuilder<TData> SideEffect()
    {
        _definition.HasSideEffects = true;
        return this;
    }

    public void Execute(RenderPassExecute<TData> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        if (_execute is not null)
        {
            throw new InvalidOperationException(
                $"Render pass '{_definition.Name}' already has an execute callback.");
        }

        _execute = execute;
    }

    internal IRenderPassExecutor BuildExecutor()
    {
        if (_execute is null)
        {
            throw new InvalidOperationException(
                $"Render pass '{_definition.Name}' has no execute callback.");
        }

        return new RenderPassExecutor<TData>(_data, _execute);
    }

    private static string ValidatePassName(string passName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passName);
        return passName;
    }
}

internal interface IRenderPassExecutor
{
    void Execute(ref RenderGraphCommandContext commands);
}

internal sealed class RenderPassExecutor<TData>(
    TData data,
    RenderPassExecute<TData> execute) : IRenderPassExecutor
    where TData : struct
{
    private readonly TData _data = data;
    private readonly RenderPassExecute<TData> _execute = execute;

    public void Execute(ref RenderGraphCommandContext commands) =>
        _execute(ref commands, in _data);
}

internal sealed class RenderPassDefinition(int index, string name)
{
    public int Index { get; } = index;

    public string Name { get; } = name;

    public List<RenderResourceAccess> Accesses { get; } = [];

    public HashSet<string> After { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Before { get; } = new(StringComparer.Ordinal);

    public bool HasSideEffects { get; set; }

    public required IRenderPassExecutor Executor { get; set; }
}
