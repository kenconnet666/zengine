using ZEngine.Graphics;

namespace ZEngine.RenderGraph;

public sealed class RenderGraphBuilder
{
    private static long _nextGraphId;
    private readonly long _graphId = Interlocked.Increment(ref _nextGraphId);
    private readonly List<RenderResourceDefinition> _resources = [];
    private readonly List<RenderPassDefinition> _passes = [];
    private readonly HashSet<int> _outputs = [];
    private readonly HashSet<string> _passNames = new(StringComparer.Ordinal);
    private bool _compiled;

    public RenderImage<TKind> CreateImage<TKind>(
        string name,
        ImageDescriptor descriptor) =>
        CreateImageCore<TKind>(name, descriptor, imported: false);

    public RenderImage<TKind> ImportImage<TKind>(
        string name,
        ImageDescriptor descriptor,
        ImageAccess initialAccess) =>
        CreateImageCore<TKind>(
            name,
            descriptor,
            imported: true,
            initialAccess);

    public RenderBuffer<TKind> CreateBuffer<TKind>(
        string name,
        BufferDescriptor descriptor) =>
        CreateBufferCore<TKind>(name, descriptor, imported: false);

    public RenderBuffer<TKind> ImportBuffer<TKind>(
        string name,
        BufferDescriptor descriptor) =>
        CreateBufferCore<TKind>(name, descriptor, imported: true);

    public void Export<TKind>(RenderImage<TKind> image)
    {
        EnsureMutable();
        ValidateHandle(image.Handle, RenderResourceKind.Image);
        _outputs.Add(image.Handle.Index);
    }

    public void Export<TKind>(RenderBuffer<TKind> buffer)
    {
        EnsureMutable();
        ValidateHandle(buffer.Handle, RenderResourceKind.Buffer);
        _outputs.Add(buffer.Handle.Index);
    }

    public void Pass<TData>(
        string name,
        RenderPassSetup<TData> setup)
        where TData : struct
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(setup);
        if (!_passNames.Add(name))
        {
            throw new InvalidOperationException(
                $"Render pass name '{name}' is already registered.");
        }

        RenderPassDefinition definition = new(_passes.Count, name)
        {
            Executor = NullRenderPassExecutor.Instance
        };
        RenderPassBuilder<TData> builder = new(this, definition);
        setup(builder);
        definition.Executor = builder.BuildExecutor();
        _passes.Add(definition);
    }

    public CompiledRenderGraph Compile()
    {
        EnsureMutable();
        _compiled = true;
        return RenderGraphCompiler.Compile(
            _resources,
            _passes,
            _outputs);
    }

    internal void RegisterAccess(
        RenderPassDefinition pass,
        RenderResourceHandle handle,
        RenderAccessMode mode,
        ImageAccess access)
    {
        EnsureMutable();
        ValidateHandle(handle, RenderResourceKind.Image);
        ValidateAccess(pass, handle.Index, access.Stage, access.Access);
        pass.Accesses.Add(
            new(
                handle.Index,
                mode,
                access.Stage,
                access.Access,
                access.Layout,
                access.Load,
                access.Store));
    }

    internal void RegisterAccess(
        RenderPassDefinition pass,
        RenderResourceHandle handle,
        RenderAccessMode mode,
        BufferAccess access)
    {
        EnsureMutable();
        ValidateHandle(handle, RenderResourceKind.Buffer);
        ValidateAccess(pass, handle.Index, access.Stage, access.Access);
        pass.Accesses.Add(
            new(
                handle.Index,
                mode,
                access.Stage,
                access.Access,
                null,
                LoadOp.Load,
                StoreOp.Store));
    }

    private RenderImage<TKind> CreateImageCore<TKind>(
        string name,
        ImageDescriptor descriptor,
        bool imported,
        ImageAccess? initialAccess = null)
    {
        EnsureMutable();
        ValidateResourceName(name);
        if (descriptor.Format == PixelFormat.Undefined)
        {
            throw new ArgumentException(
                "A render image requires a concrete pixel format.",
                nameof(descriptor));
        }

        if (descriptor.Usage == ImageUsage.None)
        {
            throw new ArgumentException(
                "A render image requires at least one usage.",
                nameof(descriptor));
        }

        if (imported && initialAccess is null)
        {
            throw new ArgumentException(
                "An imported render image requires an explicit initial access state.",
                nameof(initialAccess));
        }

        int index = _resources.Count;
        _resources.Add(
            new(
                index,
                name,
                RenderResourceKind.Image,
                descriptor,
                imported,
                initialAccess is { } state
                    ? new RenderResourceAccess(
                        index,
                        RenderAccessMode.Read,
                        state.Stage,
                        state.Access,
                        state.Layout,
                        state.Load,
                        state.Store)
                    : null));
        return new(new(_graphId, index, RenderResourceKind.Image));
    }

    private RenderBuffer<TKind> CreateBufferCore<TKind>(
        string name,
        BufferDescriptor descriptor,
        bool imported)
    {
        EnsureMutable();
        ValidateResourceName(name);
        int index = _resources.Count;
        _resources.Add(
            new(
                index,
                name,
                RenderResourceKind.Buffer,
                descriptor,
                imported,
                null));
        return new(new(_graphId, index, RenderResourceKind.Buffer));
    }

    private void ValidateHandle(
        RenderResourceHandle handle,
        RenderResourceKind expectedKind)
    {
        if (handle.IsNull
            || handle.GraphId != _graphId
            || handle.Index >= _resources.Count
            || handle.Kind != expectedKind)
        {
            throw new InvalidOperationException(
                "The render resource handle is null, stale, has the wrong kind, or belongs to another graph.");
        }
    }

    private static void ValidateAccess(
        RenderPassDefinition pass,
        int resourceIndex,
        RenderPipelineStage stage,
        RenderAccessFlags access)
    {
        if (stage == RenderPipelineStage.None || access == RenderAccessFlags.None)
        {
            throw new ArgumentException(
                "A render resource access requires non-empty stage and access masks.");
        }

        if (pass.Accesses.Exists(candidate => candidate.ResourceIndex == resourceIndex))
        {
            throw new InvalidOperationException(
                $"Render pass '{pass.Name}' accesses the same resource more than once. Use ReadWrite for a combined access.");
        }
    }

    private void ValidateResourceName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_resources.Exists(resource =>
                string.Equals(resource.Name, name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Render resource name '{name}' is already registered.");
        }
    }

    private void EnsureMutable()
    {
        if (_compiled)
        {
            throw new InvalidOperationException(
                "A render graph builder cannot be modified or compiled again after Compile().");
        }
    }

    private sealed class NullRenderPassExecutor : IRenderPassExecutor
    {
        public static NullRenderPassExecutor Instance { get; } = new();

        public void Execute(ref RenderGraphCommandContext commands)
        {
        }
    }
}
