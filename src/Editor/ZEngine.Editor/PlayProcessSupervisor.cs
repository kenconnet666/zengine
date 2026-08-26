using System.Diagnostics;

namespace ZEngine.Editor;

public sealed record PlayProcessResult(
    int ProcessId,
    int ExitCode,
    bool Crashed,
    TimeSpan Duration,
    IReadOnlyList<string> StandardOutput,
    IReadOnlyList<string> StandardError);

public sealed class PlayProcessSupervisor(EditorConsole console)
{
    public PlayProcessSession Start(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ProcessStartInfo start = new(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory is null
                ? Environment.CurrentDirectory
                : Path.GetFullPath(workingDirectory)
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Play process failed to start.");
        console.Write(
            EditorLogLevel.Information,
            "Play",
            $"Started process {process.Id}.");
        return new(process, console);
    }
}

public sealed class PlayProcessSession : IAsyncDisposable
{
    private readonly Process _process;
    private readonly EditorConsole _console;
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    private readonly Task<string> _standardOutput;
    private readonly Task<string> _standardError;
    private PlayProcessResult? _result;

    internal PlayProcessSession(Process process, EditorConsole console)
    {
        _process = process;
        _console = console;
        _standardOutput = process.StandardOutput.ReadToEndAsync();
        _standardError = process.StandardError.ReadToEndAsync();
    }

    public int ProcessId => _process.Id;

    public bool HasExited => _process.HasExited;

    public async Task<PlayProcessResult> WaitForExitAsync(
        CancellationToken cancellationToken = default)
    {
        if (_result is not null)
        {
            return _result;
        }

        await _process.WaitForExitAsync(cancellationToken);
        string output = await _standardOutput;
        string error = await _standardError;
        int exitCode = _process.ExitCode;
        _result = new(
            _process.Id,
            exitCode,
            exitCode != 0,
            DateTimeOffset.UtcNow - _started,
            SplitLines(output),
            SplitLines(error));
        _console.Write(
            exitCode == 0 ? EditorLogLevel.Information : EditorLogLevel.Error,
            "Play",
            exitCode == 0
                ? $"Process {_process.Id} exited normally."
                : $"Process {_process.Id} crashed with exit code {exitCode}.");
        return _result;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    private static string[] SplitLines(string text) =>
        text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
