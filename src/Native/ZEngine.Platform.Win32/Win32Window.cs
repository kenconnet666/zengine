using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZEngine.Platform.Win32;

public sealed unsafe class Win32Window : INativeWindow
{
    private const string ClassName = "ZEngine.Window";
    private static readonly object RegistrationLock = new();
    private static readonly Lazy<bool> DpiAwareness = new(EnableDpiAwareness);
    private static ushort _classAtom;
    private nint _window;

    private Win32Window(
        nint window,
        nint instance)
    {
        _window = window;
        NativeInstance = instance;
    }

    public nint NativeHandle => _window;

    public nint NativeInstance { get; }

    public WindowSize ClientSize
    {
        get
        {
            ObjectDisposedException.ThrowIf(_window == 0, this);
            if (!Win32Native.GetClientRect(_window, out Rect rectangle))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "GetClientRect failed.");
            }

            return new(
                checked((uint)Math.Max(0, rectangle.Right - rectangle.Left)),
                checked((uint)Math.Max(0, rectangle.Bottom - rectangle.Top)));
        }
    }

    public WindowDpi Dpi
    {
        get
        {
            ObjectDisposedException.ThrowIf(_window == 0, this);
            uint dpi = Win32Native.GetDpiForWindow(_window);
            dpi = dpi == 0 ? WindowDpi.Default : dpi;
            return new(dpi, dpi);
        }
    }

    public bool IsMinimized =>
        _window != 0 && Win32Native.IsIconic(_window);

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
        _ = DpiAwareness.Value;
        nint instance = Win32Native.GetModuleHandle(null);
        EnsureWindowClass(instance);

        uint dpi = Win32Native.GetDpiForSystem();
        dpi = dpi == 0 ? WindowDpi.Default : dpi;
        Rect windowBounds = new()
        {
            Right = checked((int)MathF.Round(width * dpi / 96f)),
            Bottom = checked((int)MathF.Round(height * dpi / 96f))
        };
        if (!Win32Native.AdjustWindowRectExForDpi(
                ref windowBounds,
                Win32Native.WsOverlappedWindow,
                false,
                0,
                dpi))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "AdjustWindowRectExForDpi failed during window creation.");
        }

        nint window = Win32Native.CreateWindowEx(
            0,
            ClassName,
            title,
            Win32Native.WsOverlappedWindow,
            Win32Native.CwUseDefault,
            Win32Native.CwUseDefault,
            windowBounds.Right - windowBounds.Left,
            windowBounds.Bottom - windowBounds.Top,
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

        Win32Window result = new(window, instance);
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

        return _window != 0 && Win32Native.IsWindow(_window);
    }

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_window == 0, this);
        Win32Native.ShowWindow(_window, Win32Native.SwShow);
    }

    public void ResizeClient(uint width, uint height)
    {
        ObjectDisposedException.ThrowIf(_window == 0, this);
        ArgumentOutOfRangeException.ThrowIfZero(width);
        ArgumentOutOfRangeException.ThrowIfZero(height);

        Rect rectangle = new()
        {
            Right = checked((int)width),
            Bottom = checked((int)height)
        };
        if (!Win32Native.AdjustWindowRectExForDpi(
                ref rectangle,
                Win32Native.WsOverlappedWindow,
                false,
                0,
                Dpi.X))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "AdjustWindowRectExForDpi failed.");
        }

        if (!Win32Native.SetWindowPos(
                _window,
                0,
                0,
                0,
                rectangle.Right - rectangle.Left,
                rectangle.Bottom - rectangle.Top,
                Win32Native.SwpNoMove
                | Win32Native.SwpNoZOrder
                | Win32Native.SwpNoActivate))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "SetWindowPos failed.");
        }
    }

    public void Minimize()
    {
        ObjectDisposedException.ThrowIf(_window == 0, this);
        Win32Native.ShowWindow(_window, Win32Native.SwMinimize);
    }

    public void Restore()
    {
        ObjectDisposedException.ThrowIf(_window == 0, this);
        Win32Native.ShowWindow(_window, Win32Native.SwRestore);
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

    private static bool EnableDpiAwareness()
    {
        if (Win32Native.SetProcessDpiAwarenessContext(
                Win32Native.DpiAwarenessContextPerMonitorAwareV2))
        {
            return true;
        }

        int error = Marshal.GetLastPInvokeError();
        const int accessDenied = 5;
        if (error == accessDenied)
        {
            return false;
        }

        throw new Win32Exception(
            error,
            "SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2) failed.");
    }
}
