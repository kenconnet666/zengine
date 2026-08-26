using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

public sealed unsafe class VulkanSurface : IDisposable
{
    private readonly VkInstance _instance;
    private readonly delegate* unmanaged<VkInstance, VkSurfaceKHR, void*, void>
        _destroySurface;
    private readonly delegate* unmanaged<VkPhysicalDevice, uint, VkSurfaceKHR, uint*, VkResult>
        _getSurfaceSupport;
    private readonly delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, VkSurfaceCapabilitiesKhr*, VkResult>
        _getSurfaceCapabilities;
    private readonly delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, uint*, VkSurfaceFormatKhr*, VkResult>
        _getSurfaceFormats;
    private readonly delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, uint*, VkPresentModeKhr*, VkResult>
        _getPresentModes;
    private VkSurfaceKHR _handle;

    private VulkanSurface(
        VkInstance instance,
        VkSurfaceKHR handle,
        delegate* unmanaged<VkInstance, VkSurfaceKHR, void*, void> destroySurface,
        delegate* unmanaged<VkPhysicalDevice, uint, VkSurfaceKHR, uint*, VkResult> getSurfaceSupport,
        delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, VkSurfaceCapabilitiesKhr*, VkResult> getSurfaceCapabilities,
        delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, uint*, VkSurfaceFormatKhr*, VkResult> getSurfaceFormats,
        delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, uint*, VkPresentModeKhr*, VkResult> getPresentModes)
    {
        _instance = instance;
        _handle = handle;
        _destroySurface = destroySurface;
        _getSurfaceSupport = getSurfaceSupport;
        _getSurfaceCapabilities = getSurfaceCapabilities;
        _getSurfaceFormats = getSurfaceFormats;
        _getPresentModes = getPresentModes;
    }

    public VkSurfaceKHR Handle => _handle;

    internal static VulkanSurface CreateWin32(
        VulkanLoader loader,
        VkInstance instance,
        nint nativeInstance,
        nint nativeWindow)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "A Win32 Vulkan surface can only be created on Windows.");
        }

        var createSurface =
            (delegate* unmanaged<VkInstance, VkWin32SurfaceCreateInfoKhr*, void*, VkSurfaceKHR*, VkResult>)RequireProc(
                loader,
                instance,
                "vkCreateWin32SurfaceKHR\0"u8);

        VkWin32SurfaceCreateInfoKhr createInfo = new()
        {
            SType = VkStructureType.Win32SurfaceCreateInfoKhr,
            Instance = nativeInstance,
            Window = nativeWindow
        };

        VkSurfaceKHR handle = default;
        VulkanException.ThrowIfFailed(
            createSurface(instance, &createInfo, null, &handle),
            "vkCreateWin32SurfaceKHR");

        return new(
            instance,
            handle,
            (delegate* unmanaged<VkInstance, VkSurfaceKHR, void*, void>)RequireProc(
                loader,
                instance,
                "vkDestroySurfaceKHR\0"u8),
            (delegate* unmanaged<VkPhysicalDevice, uint, VkSurfaceKHR, uint*, VkResult>)RequireProc(
                loader,
                instance,
                "vkGetPhysicalDeviceSurfaceSupportKHR\0"u8),
            (delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, VkSurfaceCapabilitiesKhr*, VkResult>)RequireProc(
                loader,
                instance,
                "vkGetPhysicalDeviceSurfaceCapabilitiesKHR\0"u8),
            (delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, uint*, VkSurfaceFormatKhr*, VkResult>)RequireProc(
                loader,
                instance,
                "vkGetPhysicalDeviceSurfaceFormatsKHR\0"u8),
            (delegate* unmanaged<VkPhysicalDevice, VkSurfaceKHR, uint*, VkPresentModeKhr*, VkResult>)RequireProc(
                loader,
                instance,
                "vkGetPhysicalDeviceSurfacePresentModesKHR\0"u8));
    }

    public bool SupportsPresentation(
        VulkanPhysicalDeviceInfo physicalDevice)
    {
        ThrowIfDisposed();

        uint supported = 0;
        VulkanException.ThrowIfFailed(
            _getSurfaceSupport(
                physicalDevice.Handle,
                physicalDevice.GraphicsQueueFamilyIndex,
                _handle,
                &supported),
            "vkGetPhysicalDeviceSurfaceSupportKHR");

        return supported != 0;
    }

    public VulkanSurfaceSupport QuerySupport(
        VulkanPhysicalDeviceInfo physicalDevice)
    {
        ThrowIfDisposed();

        VkSurfaceCapabilitiesKhr capabilities = default;
        VulkanException.ThrowIfFailed(
            _getSurfaceCapabilities(
                physicalDevice.Handle,
                _handle,
                &capabilities),
            "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");

        VkSurfaceFormatKhr[] formats =
            QueryFormats(physicalDevice.Handle);
        VkPresentModeKhr[] presentModes =
            QueryPresentModes(physicalDevice.Handle);

        return new(capabilities, formats, presentModes);
    }

    public VulkanSwapchain CreateSwapchain(
        VulkanDevice device,
        VulkanPhysicalDeviceInfo physicalDevice,
        uint requestedWidth,
        uint requestedHeight)
    {
        VulkanSurfaceSupport support = QuerySupport(physicalDevice);
        return VulkanSwapchain.Create(
            device,
            this,
            support,
            requestedWidth,
            requestedHeight);
    }

    public void Dispose()
    {
        VkSurfaceKHR handle = _handle;
        if (!handle.IsNull)
        {
            _handle = default;
            _destroySurface(_instance, handle, null);
        }
    }

    private VkSurfaceFormatKhr[] QueryFormats(
        VkPhysicalDevice physicalDevice)
    {
        uint count = 0;
        VulkanException.ThrowIfFailed(
            _getSurfaceFormats(
                physicalDevice,
                _handle,
                &count,
                null),
            "vkGetPhysicalDeviceSurfaceFormatsKHR(count)");

        VkSurfaceFormatKhr[] values = new VkSurfaceFormatKhr[count];
        fixed (VkSurfaceFormatKhr* pointer = values)
        {
            VulkanException.ThrowIfFailedOrIncomplete(
                _getSurfaceFormats(
                    physicalDevice,
                    _handle,
                    &count,
                    pointer),
                "vkGetPhysicalDeviceSurfaceFormatsKHR(values)");
        }

        return values.AsSpan(0, (int)count).ToArray();
    }

    private VkPresentModeKhr[] QueryPresentModes(
        VkPhysicalDevice physicalDevice)
    {
        uint count = 0;
        VulkanException.ThrowIfFailed(
            _getPresentModes(
                physicalDevice,
                _handle,
                &count,
                null),
            "vkGetPhysicalDeviceSurfacePresentModesKHR(count)");

        VkPresentModeKhr[] values = new VkPresentModeKhr[count];
        fixed (VkPresentModeKhr* pointer = values)
        {
            VulkanException.ThrowIfFailedOrIncomplete(
                _getPresentModes(
                    physicalDevice,
                    _handle,
                    &count,
                    pointer),
                "vkGetPhysicalDeviceSurfacePresentModesKHR(values)");
        }

        return values.AsSpan(0, (int)count).ToArray();
    }

    private static nint RequireProc(
        VulkanLoader loader,
        VkInstance instance,
        ReadOnlySpan<byte> name)
    {
        nint proc = loader.GetInstanceProcAddress(instance, name);
        return proc != 0
            ? proc
            : throw new MissingMethodException(
                "The Vulkan instance is missing a required surface command.");
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
}

public sealed record VulkanSurfaceSupport(
    VkSurfaceCapabilitiesKhr Capabilities,
    IReadOnlyList<VkSurfaceFormatKhr> Formats,
    IReadOnlyList<VkPresentModeKhr> PresentModes);
