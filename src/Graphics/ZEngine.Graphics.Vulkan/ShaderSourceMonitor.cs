namespace ZEngine.Graphics.Vulkan;

public sealed class ShaderSourceMonitor
{
    private readonly DxcShaderCompiler _compiler;
    private readonly VulkanWindowRenderer _renderer;
    private readonly string _sourcePath;
    private readonly string _vertexEntryPoint;
    private readonly string _fragmentEntryPoint;
    private readonly string _fragmentRuntimeEntryPoint;
    private DateTime _observedWriteTimeUtc;

    public ShaderSourceMonitor(
        DxcShaderCompiler compiler,
        VulkanWindowRenderer renderer,
        string sourcePath,
        string vertexEntryPoint = "VSMain",
        string fragmentEntryPoint = "PSMain",
        string fragmentRuntimeEntryPoint = "PSMain")
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(renderer);
        _compiler = compiler;
        _renderer = renderer;
        _sourcePath = Path.GetFullPath(sourcePath);
        _vertexEntryPoint = vertexEntryPoint;
        _fragmentEntryPoint = fragmentEntryPoint;
        _fragmentRuntimeEntryPoint = fragmentRuntimeEntryPoint;
        _observedWriteTimeUtc = File.GetLastWriteTimeUtc(_sourcePath);
    }

    public async Task<ShaderReloadResult> CheckForChangesAsync(
        CancellationToken cancellationToken = default)
    {
        DateTime writeTimeUtc = File.GetLastWriteTimeUtc(_sourcePath);
        if (writeTimeUtc <= _observedWriteTimeUtc)
        {
            return new(
                ShaderReloadStatus.Unchanged,
                _renderer.ShaderGeneration);
        }

        _observedWriteTimeUtc = writeTimeUtc;
        ShaderCompilationResult compilation = await _compiler.CompileAsync(
            _sourcePath,
            _vertexEntryPoint,
            _fragmentEntryPoint,
            _fragmentRuntimeEntryPoint,
            cancellationToken);
        if (!compilation.Succeeded)
        {
            return new(
                ShaderReloadStatus.Rejected,
                _renderer.ShaderGeneration,
                compilation.Diagnostics);
        }

        return _renderer.ReloadShaders(
            compilation.VertexShader!,
            compilation.FragmentShader!);
    }
}
