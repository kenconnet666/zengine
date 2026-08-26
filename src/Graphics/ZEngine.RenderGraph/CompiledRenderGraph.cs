namespace ZEngine.RenderGraph;

public enum CompiledResourceKind
{
    Image,
    Buffer
}

public readonly record struct RenderBarrier(
    string Resource,
    CompiledResourceKind ResourceKind,
    string SourcePass,
    string DestinationPass,
    RenderPipelineStage SourceStage,
    RenderAccessFlags SourceAccess,
    RenderImageLayout? OldLayout,
    RenderPipelineStage DestinationStage,
    RenderAccessFlags DestinationAccess,
    RenderImageLayout? NewLayout);

public readonly record struct ResourceLifetime(
    string Resource,
    CompiledResourceKind ResourceKind,
    int FirstUse,
    int LastUse,
    bool Transient,
    int AliasSlot);

public enum CompiledAccessMode
{
    Read,
    Write,
    ReadWrite
}

public readonly record struct CompiledResourceAccess(
    int ResourceIndex,
    string Resource,
    CompiledResourceKind ResourceKind,
    CompiledAccessMode Mode,
    RenderPipelineStage Stage,
    RenderAccessFlags Access,
    RenderImageLayout? Layout,
    LoadOp Load,
    StoreOp Store);

public sealed class CompiledRenderPass
{
    internal CompiledRenderPass(
        string name,
        int originalIndex,
        string[] dependencies,
        CompiledResourceAccess[] accesses,
        IRenderPassExecutor executor)
    {
        Name = name;
        OriginalIndex = originalIndex;
        Dependencies = dependencies;
        Accesses = accesses;
        Executor = executor;
    }

    public string Name { get; }

    public int OriginalIndex { get; }

    public IReadOnlyList<string> Dependencies { get; }

    public IReadOnlyList<CompiledResourceAccess> Accesses { get; }

    internal IRenderPassExecutor Executor { get; }
}

public sealed record RenderRecordingBatch(
    int Level,
    IReadOnlyList<CompiledRenderPass> Passes);

public interface IRenderGraphCommandSink
{
    void ApplyBarrier(in RenderBarrier barrier);

    void BeginPass(CompiledRenderPass pass);

    void Command(string passName, string command);

    void EndPass(CompiledRenderPass pass);
}

public struct RenderGraphCommandContext
{
    private readonly IRenderGraphCommandSink _sink;

    internal RenderGraphCommandContext(IRenderGraphCommandSink sink)
    {
        _sink = sink;
        PassName = string.Empty;
    }

    public string PassName { get; internal set; }

    public readonly void Emit(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        _sink.Command(PassName, command);
    }

    public readonly TBackend GetBackend<TBackend>()
        where TBackend : class
    {
        if (_sink is TBackend backend)
        {
            return backend;
        }

        throw new NotSupportedException(
            $"The active render graph sink does not implement {typeof(TBackend).FullName}.");
    }
}

public sealed class NullRenderGraphCommandSink : IRenderGraphCommandSink
{
    public static NullRenderGraphCommandSink Instance { get; } = new();

    private NullRenderGraphCommandSink()
    {
    }

    public void ApplyBarrier(in RenderBarrier barrier)
    {
    }

    public void BeginPass(CompiledRenderPass pass)
    {
    }

    public void Command(string passName, string command)
    {
    }

    public void EndPass(CompiledRenderPass pass)
    {
    }
}

public sealed class CompiledRenderGraph
{
    private readonly RenderBarrier[][] _barriersByPass;

    internal CompiledRenderGraph(
        CompiledRenderPass[] passes,
        RenderBarrier[] barriers,
        RenderBarrier[][] barriersByPass,
        ResourceLifetime[] resourceLifetimes,
        RenderRecordingBatch[] recordingBatches,
        string[] culledPasses)
    {
        Passes = passes;
        Barriers = barriers;
        _barriersByPass = barriersByPass;
        ResourceLifetimes = resourceLifetimes;
        RecordingBatches = recordingBatches;
        CulledPasses = culledPasses;
    }

    public IReadOnlyList<CompiledRenderPass> Passes { get; }

    public IReadOnlyList<RenderBarrier> Barriers { get; }

    public IReadOnlyList<ResourceLifetime> ResourceLifetimes { get; }

    public IReadOnlyList<RenderRecordingBatch> RecordingBatches { get; }

    public IReadOnlyList<string> CulledPasses { get; }

    public void Execute(IRenderGraphCommandSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        RenderGraphCommandContext context = new(sink);

        for (int passIndex = 0; passIndex < Passes.Count; passIndex++)
        {
            RenderBarrier[] passBarriers = _barriersByPass[passIndex];
            for (int barrierIndex = 0; barrierIndex < passBarriers.Length; barrierIndex++)
            {
                sink.ApplyBarrier(in passBarriers[barrierIndex]);
            }

            CompiledRenderPass pass = Passes[passIndex];
            context.PassName = pass.Name;
            sink.BeginPass(pass);
            pass.Executor.Execute(ref context);
            sink.EndPass(pass);
        }
    }
}

public sealed class RenderGraphCompileException(string message)
    : InvalidOperationException(message);
