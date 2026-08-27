using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZEngine.Platform.Win32;

internal static unsafe partial class Win32Native
{
    internal const uint CsOwnDc = 0x0020;
    internal const uint WsOverlappedWindow = 0x00CF0000;
    internal const int CwUseDefault = unchecked((int)0x80000000);
    internal const int SwShow = 5;
    internal const int SwMinimize = 6;
    internal const int SwRestore = 9;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint PmRemove = 0x0001;
    internal const uint WmClose = 0x0010;
    internal const uint WmDestroy = 0x0002;
    internal const uint WmQuit = 0x0012;
    internal const uint WmDpiChanged = 0x02E0;
    internal const uint ColorWindow = 5;
    internal const int IdcArrow = 32512;
    internal static nint DpiAwarenessContextPerMonitorAwareV2 => new(-4);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW",
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW",
        SetLastError = true)]
    internal static partial ushort RegisterClassEx(
        WndClassEx* windowClass);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW",
        SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [LibraryImport("user32.dll", EntryPoint = "DestroyWindow",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "IsWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll", EntryPoint = "GetClientRect",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(
        nint window,
        out Rect rectangle);

    [LibraryImport("user32.dll", EntryPoint = "AdjustWindowRectExForDpi",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AdjustWindowRectExForDpi(
        ref Rect rectangle,
        uint style,
        [MarshalAs(UnmanagedType.Bool)] bool hasMenu,
        uint extendedStyle,
        uint dpi);

    [LibraryImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetProcessDpiAwarenessContext(nint value);

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    internal static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForSystem")]
    internal static partial uint GetDpiForSystem();

    [LibraryImport("user32.dll", EntryPoint = "SetWindowPos",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [LibraryImport("user32.dll", EntryPoint = "IsIconic")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PeekMessage(
        out Message message,
        nint window,
        uint minimumMessage,
        uint maximumMessage,
        uint removeMessage);

    [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(in Message message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(in Message message);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(
        nint window,
        uint message,
        nuint wParam,
        nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "PostQuitMessage")]
    internal static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    internal static partial nint LoadCursor(
        nint instance,
        nint cursorName);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    internal static nint WindowProcedure(
        nint window,
        uint message,
        nuint wParam,
        nint lParam)
    {
        switch (message)
        {
            case WmDpiChanged:
                if (lParam != 0)
                {
                    Rect suggested = *(Rect*)lParam;
                    _ = SetWindowPos(
                        window,
                        0,
                        suggested.Left,
                        suggested.Top,
                        suggested.Right - suggested.Left,
                        suggested.Bottom - suggested.Top,
                        SwpNoZOrder | SwpNoActivate);
                }

                return 0;
            case WmClose:
                DestroyWindow(window);
                return 0;
            case WmDestroy:
                return 0;
            default:
                return DefWindowProc(window, message, wParam, lParam);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WndClassEx
{
    internal uint Size;
    internal uint Style;
    internal delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>
        WindowProcedure;
    internal int ClassExtra;
    internal int WindowExtra;
    internal nint Instance;
    internal nint Icon;
    internal nint Cursor;
    internal nint BackgroundBrush;
    internal char* MenuName;
    internal char* ClassName;
    internal nint SmallIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Point
{
    internal int X;
    internal int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    internal int Left;
    internal int Top;
    internal int Right;
    internal int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Message
{
    internal nint Window;
    internal uint Value;
    internal nuint WParam;
    internal nint LParam;
    internal uint Time;
    internal Point Point;
    internal uint Private;
}
