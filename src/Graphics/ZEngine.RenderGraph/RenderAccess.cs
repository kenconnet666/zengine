namespace ZEngine.RenderGraph;

[Flags]
public enum RenderPipelineStage : ulong
{
    None = 0,
    Top = 1UL << 0,
    DrawIndirect = 1UL << 1,
    VertexInput = 1UL << 2,
    VertexShader = 1UL << 3,
    FragmentShader = 1UL << 4,
    EarlyDepthStencil = 1UL << 5,
    LateDepthStencil = 1UL << 6,
    ColorAttachmentOutput = 1UL << 7,
    ComputeShader = 1UL << 8,
    Transfer = 1UL << 9,
    Bottom = 1UL << 10,
    Host = 1UL << 11,
    AllGraphics = 1UL << 12,
    AllCommands = 1UL << 13
}

[Flags]
public enum RenderAccessFlags : ulong
{
    None = 0,
    IndirectCommandRead = 1UL << 0,
    IndexRead = 1UL << 1,
    VertexAttributeRead = 1UL << 2,
    UniformRead = 1UL << 3,
    ShaderRead = 1UL << 4,
    ShaderWrite = 1UL << 5,
    ColorAttachmentRead = 1UL << 6,
    ColorAttachmentWrite = 1UL << 7,
    DepthStencilRead = 1UL << 8,
    DepthStencilWrite = 1UL << 9,
    TransferRead = 1UL << 10,
    TransferWrite = 1UL << 11,
    HostRead = 1UL << 12,
    HostWrite = 1UL << 13,
    MemoryRead = 1UL << 14,
    MemoryWrite = 1UL << 15
}

public enum RenderImageLayout
{
    Undefined,
    General,
    ColorAttachment,
    DepthStencilAttachment,
    DepthStencilReadOnly,
    ShaderReadOnly,
    TransferSource,
    TransferDestination,
    Present
}

public enum LoadOp
{
    Load,
    Clear,
    DontCare
}

public enum StoreOp
{
    Store,
    DontCare
}

public readonly record struct ImageAccess(
    RenderPipelineStage Stage,
    RenderAccessFlags Access,
    RenderImageLayout Layout,
    LoadOp Load = LoadOp.Load,
    StoreOp Store = StoreOp.Store)
{
    public static ImageAccess Sampled(
        RenderPipelineStage stage = RenderPipelineStage.FragmentShader) =>
        new(stage, RenderAccessFlags.ShaderRead, RenderImageLayout.ShaderReadOnly);

    public static ImageAccess ColorAttachment(
        LoadOp load = LoadOp.Load,
        StoreOp store = StoreOp.Store) =>
        new(
            RenderPipelineStage.ColorAttachmentOutput,
            RenderAccessFlags.ColorAttachmentRead
            | RenderAccessFlags.ColorAttachmentWrite,
            RenderImageLayout.ColorAttachment,
            load,
            store);

    public static ImageAccess DepthAttachment(
        LoadOp load = LoadOp.Load,
        StoreOp store = StoreOp.Store) =>
        new(
            RenderPipelineStage.EarlyDepthStencil
            | RenderPipelineStage.LateDepthStencil,
            RenderAccessFlags.DepthStencilRead
            | RenderAccessFlags.DepthStencilWrite,
            RenderImageLayout.DepthStencilAttachment,
            load,
            store);

    public static ImageAccess Storage(
        RenderPipelineStage stage = RenderPipelineStage.ComputeShader) =>
        new(
            stage,
            RenderAccessFlags.ShaderRead | RenderAccessFlags.ShaderWrite,
            RenderImageLayout.General);

    public static ImageAccess TransferSource() =>
        new(
            RenderPipelineStage.Transfer,
            RenderAccessFlags.TransferRead,
            RenderImageLayout.TransferSource);

    public static ImageAccess TransferDestination() =>
        new(
            RenderPipelineStage.Transfer,
            RenderAccessFlags.TransferWrite,
            RenderImageLayout.TransferDestination);

    public static ImageAccess Present() =>
        new(
            RenderPipelineStage.Bottom,
            RenderAccessFlags.MemoryRead,
            RenderImageLayout.Present);
}

public readonly record struct BufferAccess(
    RenderPipelineStage Stage,
    RenderAccessFlags Access)
{
    public static BufferAccess Vertex() =>
        new(
            RenderPipelineStage.VertexInput,
            RenderAccessFlags.VertexAttributeRead);

    public static BufferAccess Index() =>
        new(RenderPipelineStage.VertexInput, RenderAccessFlags.IndexRead);

    public static BufferAccess Uniform(
        RenderPipelineStage stage =
            RenderPipelineStage.VertexShader | RenderPipelineStage.FragmentShader) =>
        new(stage, RenderAccessFlags.UniformRead);

    public static BufferAccess Storage(
        RenderPipelineStage stage = RenderPipelineStage.ComputeShader) =>
        new(
            stage,
            RenderAccessFlags.ShaderRead | RenderAccessFlags.ShaderWrite);

    public static BufferAccess TransferSource() =>
        new(RenderPipelineStage.Transfer, RenderAccessFlags.TransferRead);

    public static BufferAccess TransferDestination() =>
        new(RenderPipelineStage.Transfer, RenderAccessFlags.TransferWrite);
}

internal enum RenderAccessMode
{
    Read,
    Write,
    ReadWrite
}

internal readonly record struct RenderResourceAccess(
    int ResourceIndex,
    RenderAccessMode Mode,
    RenderPipelineStage Stage,
    RenderAccessFlags Access,
    RenderImageLayout? Layout,
    LoadOp Load,
    StoreOp Store)
{
    public bool Writes => Mode is RenderAccessMode.Write or RenderAccessMode.ReadWrite;
}
