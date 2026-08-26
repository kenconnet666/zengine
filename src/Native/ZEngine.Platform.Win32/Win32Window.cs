using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZEngine.Platform.Win32;

public sealed unsafe class Win32Window : INativeWindow
{
    private const string ClassName = "ZEngine.Window";
    private static readonly object RegistrationLock = new();
    private static ushort _classAtom;
    private nint _window;

    private Win32Window(
        nint window,
        nint instance,
        WindowSize clientSize)
    {
        _window = window;
        NativeInstance = instance;
        ClientSize = clientSize;
    }

    public nint NativeHandle => _window;

    public nint NativeInstance { get; }

    public WindowSize ClientSize { get; }

    public static Win32Window Create(
        string title,
        uint width,
        uint height,
        bool visible = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Win32Window is only available on Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        nint instance = Win32Native.GetModuleHandle(null);
        EnsureWindowClass(instance);

        nint window = Win32Native.CreateWindowEx(
            0,
            ClassName,
            title,
            Win32Native.WsOverlappedWindow,
            Win32Native.CwUseDefault,
            Win32Native.CwUseDefault,
            checked((int)width),
            checked((int)height),
            0,
            0,
            instance,
            0);

        if (window == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "CreateWindowExW failed.");
        }

        Win32Window result = new(window, instance, new(width, height));
        if (visible)
        {
            result.Show();
        }

        return result;
    }

    public bool PumpEvents()
    {
        while (Win32Native.PeekMessage(
                   out Message message,
                   0,
                   0,
                   0,
                   Win32Native.PmRemove))
        {
            if (message.Value == Win32Native.WmQuit)
            {
                return false;
            }

            Win32Native.TranslateMessage(in message);
            Win32Native.DispatchMessage(in message);
        }

        return _window != 0;
    }

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_window == 0, this);
        Win32Native.ShowWindow(_window, Win32Native.SwShow);
    }

    public void Dispose()
    {
        nint window = Interlocked.Exchange(ref _window, 0);
        if (window != 0)
        {
            Win32Native.DestroyWindow(window);
        }
    }

    private static void EnsureWindowClass(nint instance)
    {
        if (_classAtom != 0)
        {
            return;
        }

        lock (RegistrationLock)
        {
            if (_classAtom != 0)
            {
                return;
            }

            fixed (char* className = ClassName)
            {
                WndClassEx windowClass = new()
                {
                    Size = (uint)sizeof(WndClassEx),
                    Style = Win32Native.CsOwnDc,
                    WindowProcedure = &Win32Native.WindowProcedure,
                    Instance = instance,
                    Cursor = Win32Native.LoadCursor(
                        0,
                        Win32Native.IdcArrow),
                    BackgroundBrush =
                        (nint)(Win32Native.ColorWindow + 1),
                    ClassName = className
                };

                _classAtom = Win32Native.RegisterClassEx(&windowClass);
            }

            if (_classAtom == 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "RegisterClassExW failed.");
            }
        }
    }
}
