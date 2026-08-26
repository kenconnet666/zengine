using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ZEngine.Vulkan.Raw;

public sealed unsafe class VulkanLoader : IDisposable
{
    private readonly delegate* unmanaged<nint, byte*, nint> _getInstanceProcAddr;
    private nint _library;

    private VulkanLoader(nint library)
    {
        _library = library;
        nint export = NativeLibrary.GetExport(library, "vkGetInstanceProcAddr");
        _getInstanceProcAddr =
            (delegate* unmanaged<nint, byte*, nint>)export;
    }

    public static VulkanLoader OpenSystem()
    {
        if (TryOpen(out VulkanLoader? loader))
        {
            return loader;
        }

        throw new DllNotFoundException(
            $"No Vulkan loader was found. Tried: {string.Join(", ", CandidateLibraryNames())}.");
    }

    public static bool TryOpen(
        [NotNullWhen(true)] out VulkanLoader? loader)
    {
        foreach (string libraryName in CandidateLibraryNames())
        {
            if (NativeLibrary.TryLoad(libraryName, out nint library))
            {
                loader = new(library);
                return true;
            }
        }

        loader = null;
        return false;
    }

    public VulkanApiVersion EnumerateInstanceVersion()
    {
        ObjectDisposedException.ThrowIf(_library == 0, this);

        nint proc = GetGlobalProc("vkEnumerateInstanceVersion\0"u8);
        if (proc == 0)
        {
            return VulkanApiVersion.Create(1, 0, 0);
        }

        var enumerate =
            (delegate* unmanaged<uint*, int>)proc;
        uint version = 0;
        int result = enumerate(&version);
        if (result != 0)
        {
            throw new InvalidOperationException(
                $"vkEnumerateInstanceVersion failed with VkResult {result}.");
        }

        return new(version);
    }

    public void Dispose()
    {
        nint library = Interlocked.Exchange(ref _library, 0);
        if (library != 0)
        {
            NativeLibrary.Free(library);
        }
    }

    private nint GetGlobalProc(ReadOnlySpan<byte> name)
    {
        if (name.IsEmpty || name[^1] != 0)
        {
            throw new ArgumentException(
                "A Vulkan function name must be null-terminated UTF-8.",
                nameof(name));
        }

        fixed (byte* pointer = name)
        {
            return _getInstanceProcAddr(0, pointer);
        }
    }

    private static string[] CandidateLibraryNames() =>
        OperatingSystem.IsWindows()
            ? ["vulkan-1.dll"]
            : OperatingSystem.IsMacOS()
                ? ["libvulkan.1.dylib", "libMoltenVK.dylib"]
                : ["libvulkan.so.1", "libvulkan.so"];
}
