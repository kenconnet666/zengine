namespace ZEngine.Graphics;

public enum PixelFormat
{
    Undefined,
    Bgra8Srgb,
    Rgba8Srgb,
    Rgba16Float,
    D32Float
}

[Flags]
public enum ImageUsage
{
    None = 0,
    ColorAttachment = 1 << 0,
    DepthStencilAttachment = 1 << 1,
    Sampled = 1 << 2,
    Storage = 1 << 3,
    TransferSource = 1 << 4,
    TransferDestination = 1 << 5,
    Present = 1 << 6
}

[Flags]
public enum BufferUsage
{
    None = 0,
    Vertex = 1 << 0,
    Index = 1 << 1,
    Uniform = 1 << 2,
    Storage = 1 << 3,
    Indirect = 1 << 4,
    TransferSource = 1 << 5,
    TransferDestination = 1 << 6
}

public enum ResourceSizeMode
{
    Absolute,
    Swapchain,
    SwapchainScaled
}

public readonly record struct ResourceSize2D(
    ResourceSizeMode Mode,
    uint Width,
    uint Height,
    float Scale)
{
    public static ResourceSize2D Absolute(uint width, uint height)
    {
        ArgumentOutOfRangeException.ThrowIfZero(width);
        ArgumentOutOfRangeException.ThrowIfZero(height);
        return new(ResourceSizeMode.Absolute, width, height, 1);
    }

    public static ResourceSize2D Swapchain { get; } =
        new(ResourceSizeMode.Swapchain, 0, 0, 1);

    public static ResourceSize2D SwapchainScaled(float scale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        return new(ResourceSizeMode.SwapchainScaled, 0, 0, scale);
    }
}

public readonly record struct ImageDescriptor(
    PixelFormat Format,
    ResourceSize2D Size,
    ImageUsage Usage,
    bool Transient = true,
    uint Samples = 1)
{
    public static ImageDescriptor Color2D(
        PixelFormat format,
        ResourceSize2D size,
        bool transient = true) =>
        new(
            format,
            size,
            ImageUsage.ColorAttachment
            | ImageUsage.Sampled
            | ImageUsage.TransferSource,
            transient);

    public static ImageDescriptor Depth2D(
        PixelFormat format,
        ResourceSize2D size,
        bool transient = true) =>
        new(
            format,
            size,
            ImageUsage.DepthStencilAttachment | ImageUsage.Sampled,
            transient);
}

public readonly record struct BufferDescriptor
{
    public BufferDescriptor(
        ulong size,
        BufferUsage usage,
        bool transient = true)
    {
        ArgumentOutOfRangeException.ThrowIfZero(size);
        ArgumentOutOfRangeException.ThrowIfEqual(usage, BufferUsage.None);
        Size = size;
        Usage = usage;
        Transient = transient;
    }

    public ulong Size { get; }

    public BufferUsage Usage { get; }

    public bool Transient { get; }
}

public enum GpuMemoryClass
{
    DeviceLocal,
    Upload,
    Readback
}

public readonly record struct GpuAllocationRequest(
    ulong Size,
    ulong Alignment,
    GpuMemoryClass MemoryClass,
    bool Transient);

public readonly record struct GpuAllocation(
    ulong Offset,
    ulong Size,
    uint MemoryType,
    nint BackendHandle);

public interface IGpuAllocator
{
    GpuAllocation Allocate(in GpuAllocationRequest request);

    void Release(in GpuAllocation allocation);
}
