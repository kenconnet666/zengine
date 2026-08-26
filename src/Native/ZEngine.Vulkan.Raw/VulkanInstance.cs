using System.Text;
using System.Runtime.InteropServices;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

public sealed unsafe class VulkanInstance : IDisposable
{
    private readonly VulkanLoader _loader;
    private readonly delegate* unmanaged<VkInstance, void*, void> _destroyInstance;
    private readonly delegate* unmanaged<VkInstance, uint*, VkPhysicalDevice*, VkResult>
        _enumeratePhysicalDevices;
    private readonly delegate* unmanaged<VkPhysicalDevice, VkPhysicalDevicePropertiesBuffer*, void>
        _getPhysicalDeviceProperties;
    private readonly delegate* unmanaged<VkPhysicalDevice, uint*, VkQueueFamilyProperties*, void>
        _getQueueFamilyProperties;
    private readonly delegate* unmanaged<VkPhysicalDevice, byte*, uint*, VkExtensionProperties*, VkResult>
        _enumerateDeviceExtensionProperties;
    private VkInstance _handle;

    private VulkanInstance(
        VulkanLoader loader,
        VkInstance handle)
    {
        _loader = loader;
        _handle = handle;
        _destroyInstance =
            (delegate* unmanaged<VkInstance, void*, void>)RequireProc(
                "vkDestroyInstance\0"u8);
        _enumeratePhysicalDevices =
            (delegate* unmanaged<VkInstance, uint*, VkPhysicalDevice*, VkResult>)RequireProc(
                "vkEnumeratePhysicalDevices\0"u8);
        _getPhysicalDeviceProperties =
            (delegate* unmanaged<VkPhysicalDevice, VkPhysicalDevicePropertiesBuffer*, void>)RequireProc(
                "vkGetPhysicalDeviceProperties\0"u8);
        _getQueueFamilyProperties =
            (delegate* unmanaged<VkPhysicalDevice, uint*, VkQueueFamilyProperties*, void>)RequireProc(
                "vkGetPhysicalDeviceQueueFamilyProperties\0"u8);
        _enumerateDeviceExtensionProperties =
            (delegate* unmanaged<VkPhysicalDevice, byte*, uint*, VkExtensionProperties*, VkResult>)RequireProc(
                "vkEnumerateDeviceExtensionProperties\0"u8);
    }

    public VkInstance Handle => _handle;

    public static VulkanInstance Create(
        VulkanLoader loader,
        VulkanInstanceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        options ??= VulkanInstanceOptions.Default;

        nint proc = loader.GetInstanceProcAddress(
            default,
            "vkCreateInstance\0"u8);
        if (proc == 0)
        {
            throw new MissingMethodException(
                "The Vulkan loader does not expose vkCreateInstance.");
        }

        var createInstance =
            (delegate* unmanaged<VkInstanceCreateInfo*, void*, VkInstance*, VkResult>)proc;

        nint[] allocatedExtensions = new nint[options.Extensions.Count];
        byte** extensionPointers =
            stackalloc byte*[Math.Max(1, options.Extensions.Count)];

        try
        {
            for (int index = 0; index < options.Extensions.Count; index++)
            {
                nint pointer = Marshal.StringToCoTaskMemUTF8(
                    options.Extensions[index]);
                allocatedExtensions[index] = pointer;
                extensionPointers[index] = (byte*)pointer;
            }

            ReadOnlySpan<byte> applicationName = "ZEngine\0"u8;
            ReadOnlySpan<byte> engineName = "ZEngine\0"u8;
            fixed (byte* applicationNamePointer = applicationName)
            fixed (byte* engineNamePointer = engineName)
            {
                VkApplicationInfo applicationInfo = new()
                {
                    SType = VkStructureType.ApplicationInfo,
                    PApplicationName = applicationNamePointer,
                    ApplicationVersion = VulkanApiVersion.Create(0, 1, 0).Value,
                    PEngineName = engineNamePointer,
                    EngineVersion = VulkanApiVersion.Create(0, 1, 0).Value,
                    ApiVersion = options.ApiVersion.Value
                };

                VkInstanceCreateInfo createInfo = new()
                {
                    SType = VkStructureType.InstanceCreateInfo,
                    PApplicationInfo = &applicationInfo,
                    EnabledExtensionCount = (uint)options.Extensions.Count,
                    PpEnabledExtensionNames =
                        options.Extensions.Count == 0 ? null : extensionPointers
                };

                VkInstance handle = default;
                VkResult result = createInstance(&createInfo, null, &handle);
                VulkanException.ThrowIfFailed(result, "vkCreateInstance");

                return new(loader, handle);
            }
        }
        finally
        {
            foreach (nint pointer in allocatedExtensions)
            {
                if (pointer != 0)
                {
                    Marshal.FreeCoTaskMem(pointer);
                }
            }
        }
    }

    public IReadOnlyList<VulkanPhysicalDeviceInfo> EnumeratePhysicalDevices()
    {
        ThrowIfDisposed();

        uint count = 0;
        VulkanException.ThrowIfFailed(
            _enumeratePhysicalDevices(_handle, &count, null),
            "vkEnumeratePhysicalDevices(count)");

        if (count == 0)
        {
            return [];
        }

        VkPhysicalDevice[] devices = new VkPhysicalDevice[count];
        fixed (VkPhysicalDevice* devicePointer = devices)
        {
            VkResult result = _enumeratePhysicalDevices(
                _handle,
                &count,
                devicePointer);
            VulkanException.ThrowIfFailedOrIncomplete(
                result,
                "vkEnumeratePhysicalDevices(values)");
        }

        List<VulkanPhysicalDeviceInfo> resultDevices = new((int)count);
        foreach (VkPhysicalDevice device in devices.AsSpan(0, (int)count))
        {
            resultDevices.Add(ReadPhysicalDevice(device));
        }

        return resultDevices;
    }

    public VulkanDevice CreateGraphicsDevice(
        VulkanPhysicalDeviceInfo physicalDevice,
        bool enableSwapchain = false)
    {
        ThrowIfDisposed();
        return VulkanDevice.Create(
            _loader,
            _handle,
            physicalDevice,
            enableSwapchain);
    }

    public VulkanSurface CreateWin32Surface(
        nint nativeInstance,
        nint nativeWindow)
    {
        ThrowIfDisposed();
        return VulkanSurface.CreateWin32(
            _loader,
            _handle,
            nativeInstance,
            nativeWindow);
    }

    public void Dispose()
    {
        VkInstance handle = _handle;
        if (!handle.IsNull)
        {
            _handle = default;
            _destroyInstance(handle, null);
        }
    }

    private VulkanPhysicalDeviceInfo ReadPhysicalDevice(
        VkPhysicalDevice device)
    {
        VkPhysicalDevicePropertiesBuffer properties = default;
        _getPhysicalDeviceProperties(device, &properties);

        uint queueFamilyIndex = FindGraphicsQueueFamily(device);
        HashSet<string> extensions = EnumerateExtensions(device);
        string name = ReadUtf8(properties.DeviceName, 256);

        return new(
            device,
            name,
            new(properties.ApiVersion),
            properties.DriverVersion,
            properties.VendorId,
            properties.DeviceId,
            properties.DeviceType,
            queueFamilyIndex,
            extensions);
    }

    private uint FindGraphicsQueueFamily(VkPhysicalDevice device)
    {
        uint count = 0;
        _getQueueFamilyProperties(device, &count, null);
        if (count == 0)
        {
            throw new InvalidOperationException(
                "The Vulkan physical device exposes no queue families.");
        }

        VkQueueFamilyProperties[] properties =
            new VkQueueFamilyProperties[count];
        fixed (VkQueueFamilyProperties* pointer = properties)
        {
            _getQueueFamilyProperties(device, &count, pointer);
        }

        for (uint index = 0; index < count; index++)
        {
            VkQueueFamilyProperties queue = properties[index];
            if (queue.QueueCount > 0
                && (queue.QueueFlags & VkQueueFlags.Graphics) != 0)
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            "The Vulkan physical device exposes no graphics queue.");
    }

    private HashSet<string> EnumerateExtensions(VkPhysicalDevice device)
    {
        uint count = 0;
        VkResult countResult = _enumerateDeviceExtensionProperties(
            device,
            null,
            &count,
            null);
        VulkanException.ThrowIfFailed(
            countResult,
            "vkEnumerateDeviceExtensionProperties(count)");

        VkExtensionProperties[] properties =
            new VkExtensionProperties[count];
        fixed (VkExtensionProperties* pointer = properties)
        {
            VkResult valueResult = _enumerateDeviceExtensionProperties(
                device,
                null,
                &count,
                pointer);
            VulkanException.ThrowIfFailedOrIncomplete(
                valueResult,
                "vkEnumerateDeviceExtensionProperties(values)");

            HashSet<string> names = new(StringComparer.Ordinal);
            for (int index = 0; index < (int)count; index++)
            {
                names.Add(ReadUtf8(pointer[index].ExtensionName, 256));
            }

            return names;
        }
    }

    private nint RequireProc(ReadOnlySpan<byte> name)
    {
        nint proc = _loader.GetInstanceProcAddress(_handle, name);
        return proc != 0
            ? proc
            : throw new MissingMethodException(
                $"The Vulkan instance does not expose '{Encoding.UTF8.GetString(name[..^1])}'.");
    }

    private static string ReadUtf8(byte* pointer, int capacity)
    {
        int length = 0;
        while (length < capacity && pointer[length] != 0)
        {
            length++;
        }

        return Encoding.UTF8.GetString(
            new ReadOnlySpan<byte>(pointer, length));
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
}
