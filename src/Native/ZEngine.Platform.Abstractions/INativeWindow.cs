namespace ZEngine.Platform;

public readonly record struct WindowSize(uint Width, uint Height);

public readonly record struct WindowDpi(uint X, uint Y)
{
    public const uint Default = 96;

    public float ScaleX => X / (float)Default;

    public float ScaleY => Y / (float)Default;
}

public interface INativeWindow : IDisposable
{
    nint NativeHandle { get; }

    nint NativeInstance { get; }

    WindowSize ClientSize { get; }

    WindowDpi Dpi { get; }

    bool PumpEvents();

    void Show();
}
