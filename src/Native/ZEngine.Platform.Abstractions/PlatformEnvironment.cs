using System.Runtime.InteropServices;

namespace ZEngine.Platform;

public enum PlatformKind
{
    Unknown,
    Browser,
    Windows,
    Linux,
    Android,
    MacOS,
    IOS
}

public sealed record PlatformEnvironment(
    PlatformKind Platform,
    Architecture ProcessArchitecture,
    string RuntimeIdentifier)
{
    public static PlatformEnvironment Current { get; } = Detect();

    private static PlatformEnvironment Detect()
    {
        PlatformKind platform = OperatingSystem.IsBrowser()
            ? PlatformKind.Browser
            : OperatingSystem.IsWindows()
                ? PlatformKind.Windows
                : OperatingSystem.IsAndroid()
                    ? PlatformKind.Android
                    : OperatingSystem.IsIOS()
                        ? PlatformKind.IOS
                        : OperatingSystem.IsMacOS()
                            ? PlatformKind.MacOS
                            : OperatingSystem.IsLinux()
                                ? PlatformKind.Linux
                                : PlatformKind.Unknown;

        return new(
            platform,
            RuntimeInformation.ProcessArchitecture,
            RuntimeInformation.RuntimeIdentifier);
    }
}
