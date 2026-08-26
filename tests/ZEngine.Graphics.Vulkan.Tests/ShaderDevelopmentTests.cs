using ZEngine.Graphics.Vulkan;
using Xunit;

namespace ZEngine.Graphics.Vulkan.Tests;

public sealed class ShaderDevelopmentTests
{
    [Fact]
    public async Task CompilesValidSourceRejectsInvalidSourceAndPersistsCacheAtomically()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string compilerPath = Path.Combine(
            repositoryRoot,
            "artifacts",
            "tools",
            "dxc",
            "bin",
            "x64",
            "dxc.exe");
        if (!File.Exists(compilerPath))
        {
            return;
        }

        DxcShaderCompiler compiler = new(compilerPath);
        ShaderCompilationResult valid = await compiler.CompileAsync(
            Path.Combine(repositoryRoot, "shaders", "triangle.hlsl"),
            fragmentEntryPoint: "PSMainAlt",
            cancellationToken: cancellationToken);
        Assert.True(valid.Succeeded, valid.Diagnostics);
        Assert.NotNull(valid.VertexShader);
        Assert.NotNull(valid.FragmentShader);
        SpirvModule.Validate(valid.VertexShader, nameof(valid.VertexShader));
        SpirvModule.Validate(valid.FragmentShader, nameof(valid.FragmentShader));

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "zengine-shader-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string invalidSource = Path.Combine(temporaryDirectory, "invalid.hlsl");
            await File.WriteAllTextAsync(
                invalidSource,
                "this is not valid HLSL",
                cancellationToken);
            ShaderCompilationResult invalid = await compiler.CompileAsync(
                invalidSource,
                cancellationToken: cancellationToken);
            Assert.False(invalid.Succeeded);
            Assert.False(string.IsNullOrWhiteSpace(invalid.Diagnostics));

            string cachePath = Path.Combine(temporaryDirectory, "pipeline.cache");
            byte[] cache = [1, 3, 3, 7, 9, 9];
            await PipelineCacheStore.SaveAtomicAsync(
                cachePath,
                cache,
                cancellationToken);
            Assert.Equal(
                cache,
                await PipelineCacheStore.LoadAsync(
                    cachePath,
                    cancellationToken));
            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
