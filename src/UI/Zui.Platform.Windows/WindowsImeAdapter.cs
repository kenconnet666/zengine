using System.Runtime.InteropServices;

namespace ZEngine.UI.Windows;

public readonly record struct ImeComposition(
    string Text,
    bool IsCommitted);

public static partial class WindowsImeAdapter
{
    private const uint CompositionString = 0x0008;
    private const uint ResultString = 0x0800;

    public static unsafe ImeComposition Read(nint window, bool committed)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        nint context = ImmGetContext(window);
        if (context == 0)
        {
            return new(string.Empty, committed);
        }

        try
        {
            uint index = committed ? ResultString : CompositionString;
            int bytes = ImmGetCompositionString(context, index, null, 0);
            if (bytes <= 0)
            {
                return new(string.Empty, committed);
            }

            char[] text = new char[bytes / sizeof(char)];
            fixed (char* pointer = text)
            {
                _ = ImmGetCompositionString(
                    context,
                    index,
                    pointer,
                    (uint)bytes);
            }

            return new(new string(text), committed);
        }
        finally
        {
            _ = ImmReleaseContext(window, context);
        }
    }

    [LibraryImport("imm32.dll", EntryPoint = "ImmGetContext")]
    private static partial nint ImmGetContext(nint window);

    [LibraryImport("imm32.dll", EntryPoint = "ImmReleaseContext")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ImmReleaseContext(nint window, nint context);

    [LibraryImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW")]
    private static unsafe partial int ImmGetCompositionString(
        nint context,
        uint index,
        void* buffer,
        uint bufferLength);
}
