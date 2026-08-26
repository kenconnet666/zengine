using System.Runtime.InteropServices;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

public sealed unsafe class VulkanDevice : IDisposable
{
    private readonly delegate* unmanaged<VkDevice, void*, void> _destroyDevice;
    private readonly delegate* unmanaged<VkDevice, byte*, nint> _getDeviceProcAddress;
    private VkDevice _handle;

    private VulkanDevice(
        VkDevice handle,
        VkQueue graphicsQueue,
        uint graphicsQueueFamilyIndex,
        delegate* unmanaged<VkDevice, void*, void> destroyDevice,
        delegate* unmanaged<VkDevice, byte*, nint> getDeviceProcAddress)
    {
        _handle = handle;
        GraphicsQueue = graphicsQueue;
        GraphicsQueueFamilyIndex = graphicsQueueFamilyIndex;
        _destroyDevice = destroyDevice;
        _getDeviceProcAddress = getDeviceProcAddress;
    }

    public VkDevice Handle => _handle;

    public VkQueue GraphicsQueue { get; }

    public uint GraphicsQueueFamilyIndex { get; }

    internal static VulkanDevice Create(
        VulkanLoader loader,
        VkInstance instance,
        VulkanPhysicalDeviceInfo physicalDevice,
        bool enableSwapchain)
    {
        nint getDeviceProc = loader.GetInstanceProcAddress(
            instance,
            "vkGetDeviceProcAddr\0"u8);
        if (getDeviceProc == 0)
        {
            throw new MissingMethodException(
                "The Vulkan instance does not expose vkGetDeviceProcAddr.");
        }

        nint createDeviceProc = loader.GetInstanceProcAddress(
            instance,
            "vkCreateDevice\0"u8);
        if (createDeviceProc == 0)
        {
            throw new MissingMethodException(
                "The Vulkan instance does not expose vkCreateDevice.");
        }

        var createDevice =
            (delegate* unmanaged<VkPhysicalDevice, VkDeviceCreateInfo*, void*, VkDevice*, VkResult>)createDeviceProc;
        var getDeviceProcAddress =
            (delegate* unmanaged<VkDevice, byte*, nint>)getDeviceProc;

        float priority = 1.0f;
        VkDeviceQueueCreateInfo queueCreateInfo = new()
        {
            SType = VkStructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = physicalDevice.GraphicsQueueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &priority
        };

        ReadOnlySpan<byte> swapchainName = "VK_KHR_swapchain\0"u8;
        fixed (byte* swapchainNamePointer = swapchainName)
        {
            byte** extensionNames = stackalloc byte*[1];
            uint extensionCount = 0;
            if (enableSwapchain)
            {
                if (!physicalDevice.Extensions.Contains("VK_KHR_swapchain"))
                {
                    throw new NotSupportedException(
                        "The physical device does not support VK_KHR_swapchain.");
                }

                extensionNames[0] = swapchainNamePointer;
                extensionCount = 1;
            }

            VkDeviceCreateInfo createInfo = new()
            {
                SType = VkStructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = extensionCount,
                PpEnabledExtensionNames =
                    extensionCount == 0 ? null : extensionNames
            };
            VkPhysicalDeviceVulkan13Features features13 = new()
            {
                SType = VkStructureType.PhysicalDeviceVulkan13Features,
                Synchronization2 = 1,
                DynamicRendering = 1
            };
            VkPhysicalDeviceTimelineSemaphoreFeatures timelineFeatures = new()
            {
                SType = VkStructureType.PhysicalDeviceTimelineSemaphoreFeatures,
                PNext = &features13,
                TimelineSemaphore = 1
            };
            createInfo.PNext = &timelineFeatures;

            VkDevice handle = default;
            VulkanException.ThrowIfFailed(
                createDevice(
                    physicalDevice.Handle,
                    &createInfo,
                    null,
                    &handle),
                "vkCreateDevice");

            nint destroyProc = GetDeviceProc(
                getDeviceProcAddress,
                handle,
                "vkDestroyDevice\0"u8);
            nint getQueueProc = GetDeviceProc(
                getDeviceProcAddress,
                handle,
                "vkGetDeviceQueue\0"u8);

            var getDeviceQueue =
                (delegate* unmanaged<VkDevice, uint, uint, VkQueue*, void>)getQueueProc;
            VkQueue queue = default;
            getDeviceQueue(
                handle,
                physicalDevice.GraphicsQueueFamilyIndex,
                0,
                &queue);

            return new(
                handle,
                queue,
                physicalDevice.GraphicsQueueFamilyIndex,
                (delegate* unmanaged<VkDevice, void*, void>)destroyProc,
                getDeviceProcAddress);
        }
    }

    public nint GetProcAddress(ReadOnlySpan<byte> name)
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        return GetDeviceProc(_getDeviceProcAddress, _handle, name);
    }

    public void Dispose()
    {
        VkDevice handle = _handle;
        if (!handle.IsNull)
        {
            _handle = default;
            _destroyDevice(handle, null);
        }
    }

    public void WaitIdle()
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        var waitIdle =
            (delegate* unmanaged<VkDevice, VkResult>)GetProcAddress(
                "vkDeviceWaitIdle\0"u8);
        VulkanException.ThrowIfFailed(
            waitIdle(_handle),
            "vkDeviceWaitIdle");
    }

    public VulkanTimeline CreateTimeline(ulong initialValue = 0) =>
        new(this, initialValue);

    private static nint GetDeviceProc(
        delegate* unmanaged<VkDevice, byte*, nint> getDeviceProcAddress,
        VkDevice device,
        ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            nint proc = getDeviceProcAddress(device, pointer);
            return proc != 0
                ? proc
                : throw new MissingMethodException(
                    $"The Vulkan device does not expose '{Marshal.PtrToStringUTF8((nint)pointer)}'.");
        }
    }
}
