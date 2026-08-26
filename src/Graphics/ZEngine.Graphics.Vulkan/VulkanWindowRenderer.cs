using ZEngine.RenderGraph;
using ZEngine.Vulkan.Raw;
using System.Security.Cryptography;

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
    private byte[] _vertexShader;
    private byte[] _fragmentShader;
    private byte[] _vertexShaderHash;
    private byte[] _fragmentShaderHash;
    private readonly VulkanPipelineCache _pipelineCache;
    private VulkanSwapchain? _swapchain;
    private VulkanTrianglePipeline? _pipeline;
    private VulkanClearRenderer? _renderer;
    private bool _disposed;

    public VulkanWindowRenderer(
        VulkanDevice device,
        VulkanPhysicalDeviceInfo physicalDevice,
        VulkanSurface surface,
        ReadOnlySpan<byte> vertexShader,
        ReadOnlySpan<byte> fragmentShader,
        ReadOnlySpan<byte> initialPipelineCache = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(physicalDevice);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentOutOfRangeException.ThrowIfZero(vertexShader.Length);
        ArgumentOutOfRangeException.ThrowIfZero(fragmentShader.Length);
        SpirvModule.Validate(vertexShader, nameof(vertexShader));
        SpirvModule.Validate(fragmentShader, nameof(fragmentShader));

        _device = device;
        _physicalDevice = physicalDevice;
        _surface = surface;
        _pipelineCache = new(device, initialPipelineCache);
        _vertexShader = vertexShader.ToArray();
        _fragmentShader = fragmentShader.ToArray();
        _vertexShaderHash = SHA256.HashData(vertexShader);
        _fragmentShaderHash = SHA256.HashData(fragmentShader);
        ShaderGeneration = 1;
    }

    public uint Generation { get; private set; }

    public uint ShaderGeneration { get; private set; }

    public VkExtent2D? Extent => _swapchain?.Extent;

    public CompiledRenderGraph? FrameGraph => _renderer?.FrameGraph;

    public byte[] GetPipelineCacheData() => _pipelineCache.Snapshot();

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

    public ShaderReloadResult ReloadShaders(
        ReadOnlySpan<byte> vertexShader,
        ReadOnlySpan<byte> fragmentShader)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte[] nextVertex;
        byte[] nextFragment;
        byte[] vertexHash;
        byte[] fragmentHash;
        try
        {
            SpirvModule.Validate(vertexShader, nameof(vertexShader));
            SpirvModule.Validate(fragmentShader, nameof(fragmentShader));
            vertexHash = SHA256.HashData(vertexShader);
            fragmentHash = SHA256.HashData(fragmentShader);
            if (vertexHash.AsSpan().SequenceEqual(_vertexShaderHash)
                && fragmentHash.AsSpan().SequenceEqual(_fragmentShaderHash))
            {
                return new(
                    ShaderReloadStatus.Unchanged,
                    ShaderGeneration);
            }

            nextVertex = vertexShader.ToArray();
            nextFragment = fragmentShader.ToArray();
        }
        catch (Exception exception)
        {
            return new(
                ShaderReloadStatus.Rejected,
                ShaderGeneration,
                exception.Message);
        }

        VulkanTrianglePipeline? candidate = null;
        try
        {
            if (_swapchain is not null)
            {
                candidate = new(
                    _device,
                    _swapchain.SurfaceFormat.Format,
                    nextVertex,
                    nextFragment,
                    _pipelineCache);
            }
        }
        catch (Exception exception)
        {
            candidate?.DisposeAfterGpuCompletion();
            return new(
                ShaderReloadStatus.Rejected,
                ShaderGeneration,
                exception.Message);
        }

        if (candidate is not null)
        {
            VulkanTrianglePipeline previous =
                _renderer!.ReplaceTrianglePipeline(candidate);
            _pipeline = candidate;
            _renderer.RetireAfterCurrentFrame(
                previous.DisposeAfterGpuCompletion);
        }

        _vertexShader = nextVertex;
        _fragmentShader = nextFragment;
        _vertexShaderHash = vertexHash;
        _fragmentShaderHash = fragmentHash;
        ShaderGeneration++;
        return new(
            ShaderReloadStatus.Applied,
            ShaderGeneration);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroyGeneration();
        _pipelineCache.Dispose();
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
                _fragmentShader,
                _pipelineCache);
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
