using ZEngine.RenderGraph;
using ZEngine.Vulkan.Raw;

namespace ZEngine.Graphics.Vulkan;

/// <summary>
/// Owns one resize-dependent swapchain generation and pauses frame submission
/// while the host reports a minimized or zero-sized client area.
/// </summary>
public sealed class VulkanWindowRenderer : IDisposable
{
    private readonly VulkanDevice _device;
    private readonly VulkanPhysicalDeviceInfo _physicalDevice;
    private readonly VulkanSurface _surface;
    private readonly byte[] _vertexShader;
    private readonly byte[] _fragmentShader;
    private VulkanSwapchain? _swapchain;
    private VulkanTrianglePipeline? _pipeline;
    private VulkanClearRenderer? _renderer;
    private bool _disposed;

    public VulkanWindowRenderer(
        VulkanDevice device,
        VulkanPhysicalDeviceInfo physicalDevice,
        VulkanSurface surface,
        ReadOnlySpan<byte> vertexShader,
        ReadOnlySpan<byte> fragmentShader)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(physicalDevice);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentOutOfRangeException.ThrowIfZero(vertexShader.Length);
        ArgumentOutOfRangeException.ThrowIfZero(fragmentShader.Length);

        _device = device;
        _physicalDevice = physicalDevice;
        _surface = surface;
        _vertexShader = vertexShader.ToArray();
        _fragmentShader = fragmentShader.ToArray();
    }

    public uint Generation { get; private set; }

    public VkExtent2D? Extent => _swapchain?.Extent;

    public CompiledRenderGraph? FrameGraph => _renderer?.FrameGraph;

    public bool Prepare(
        uint requestedWidth,
        uint requestedHeight,
        bool suspended)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (suspended || requestedWidth == 0 || requestedHeight == 0)
        {
            return false;
        }

        if (_swapchain is not null
            && _swapchain.Extent.Width == requestedWidth
            && _swapchain.Extent.Height == requestedHeight)
        {
            return true;
        }

        Recreate(requestedWidth, requestedHeight);
        return true;
    }

    public bool RenderFrame(
        uint requestedWidth,
        uint requestedHeight,
        bool suspended,
        float red,
        float green,
        float blue)
    {
        if (!Prepare(requestedWidth, requestedHeight, suspended))
        {
            return false;
        }

        try
        {
            _renderer!.RenderFrame(red, green, blue);
            return true;
        }
        catch (VulkanException exception)
            when (exception.Result is
                  VkResult.ErrorOutOfDateKhr or VkResult.SuboptimalKhr)
        {
            DestroyGeneration();
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroyGeneration();
    }

    private void Recreate(uint requestedWidth, uint requestedHeight)
    {
        DestroyGeneration();
        VulkanSwapchain swapchain = _surface.CreateSwapchain(
            _device,
            _physicalDevice,
            requestedWidth,
            requestedHeight);
        VulkanTrianglePipeline? pipeline = null;
        VulkanClearRenderer? renderer = null;
        try
        {
            pipeline = new(
                _device,
                swapchain.SurfaceFormat.Format,
                _vertexShader,
                _fragmentShader);
            renderer = new(_device, swapchain, pipeline);
        }
        catch
        {
            renderer?.Dispose();
            pipeline?.Dispose();
            swapchain.Dispose();
            throw;
        }

        _swapchain = swapchain;
        _pipeline = pipeline;
        _renderer = renderer;
        Generation++;
    }

    private void DestroyGeneration()
    {
        _renderer?.Dispose();
        _renderer = null;
        _pipeline?.Dispose();
        _pipeline = null;
        _swapchain?.Dispose();
        _swapchain = null;
    }
}
