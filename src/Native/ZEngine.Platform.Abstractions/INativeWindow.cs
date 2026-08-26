namespace ZEngine.Platform;

public readonly record struct WindowSize(uint Width, uint Height);

public interface INativeWindow : IDisposable
{
    nint NativeHandle { get; }

    nint NativeInstance { get; }

    WindowSize ClientSize { get; }

    bool PumpEvents();

    void Show();
}
