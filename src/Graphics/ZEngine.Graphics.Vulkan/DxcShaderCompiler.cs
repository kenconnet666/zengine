using System.Diagnostics;

namespace ZEngine.Graphics.Vulkan;

public sealed record ShaderCompilationResult(
    bool Succeeded,
    byte[]? VertexShader,
    byte[]? FragmentShader,
    string Diagnostics);

public sealed class DxcShaderCompiler(string compilerPath)
{
    private readonly string _compilerPath = Path.GetFullPath(compilerPath);

    public async Task<ShaderCompilationResult> CompileAsync(
        string sourcePath,
        string vertexEntryPoint = "VSMain",
        string fragmentEntryPoint = "PSMain",
        string fragmentRuntimeEntryPoint = "PSMain",
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_compilerPath))
        {
            return new(
                false,
                null,
                null,
                $"DXC executable was not found: {_compilerPath}");
        }

        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            return new(
                false,
                null,
                null,
                $"Shader source was not found: {fullSourcePath}");
        }

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "zengine-dxc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        string vertexOutput = Path.Combine(temporaryDirectory, "shader.vert.spv");
        string fragmentOutput = Path.Combine(temporaryDirectory, "shader.frag.spv");
        try
        {
            ProcessResult vertex = await RunAsync(
                fullSourcePath,
                vertexOutput,
                "vs_6_0",
                vertexEntryPoint,
                runtimeEntryPoint: null,
                cancellationToken);
            if (vertex.ExitCode != 0)
            {
                return new(false, null, null, vertex.Output);
            }

            ProcessResult fragment = await RunAsync(
                fullSourcePath,
                fragmentOutput,
                "ps_6_0",
                fragmentEntryPoint,
                fragmentRuntimeEntryPoint,
                cancellationToken);
            if (fragment.ExitCode != 0)
            {
                return new(false, null, null, fragment.Output);
            }

            byte[] vertexShader = await File.ReadAllBytesAsync(
                vertexOutput,
                cancellationToken);
            byte[] fragmentShader = await File.ReadAllBytesAsync(
                fragmentOutput,
                cancellationToken);
            SpirvModule.Validate(vertexShader, nameof(vertexShader));
            SpirvModule.Validate(fragmentShader, nameof(fragmentShader));
            return new(
                true,
                vertexShader,
                fragmentShader,
                string.Join(
                    Environment.NewLine,
                    new[] { vertex.Output, fragment.Output }
                        .Where(output => !string.IsNullOrWhiteSpace(output))));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(false, null, null, exception.Message);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    private async Task<ProcessResult> RunAsync(
        string sourcePath,
        string outputPath,
        string target,
        string entryPoint,
        string? runtimeEntryPoint,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(_compilerPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-spirv");
        startInfo.ArgumentList.Add("-fspv-target-env=vulkan1.3");
        startInfo.ArgumentList.Add("-O3");
        startInfo.ArgumentList.Add("-T");
        startInfo.ArgumentList.Add(target);
        startInfo.ArgumentList.Add("-E");
        startInfo.ArgumentList.Add(entryPoint);
        if (!string.IsNullOrWhiteSpace(runtimeEntryPoint)
            && !string.Equals(
                entryPoint,
                runtimeEntryPoint,
                StringComparison.Ordinal))
        {
            startInfo.ArgumentList.Add(
                $"-fspv-entrypoint-name={runtimeEntryPoint}");
        }

        startInfo.ArgumentList.Add("-Fo");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add(sourcePath);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("DXC process could not be started.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(
            cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(
            cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = string.Join(
            Environment.NewLine,
            new[] { await standardOutput, await standardError }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return new(process.ExitCode, output);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
