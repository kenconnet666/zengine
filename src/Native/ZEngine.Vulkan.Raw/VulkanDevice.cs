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
        VulkanDeviceCapabilities capabilities,
        delegate* unmanaged<VkDevice, void*, void> destroyDevice,
        delegate* unmanaged<VkDevice, byte*, nint> getDeviceProcAddress)
    {
        _handle = handle;
        GraphicsQueue = graphicsQueue;
        GraphicsQueueFamilyIndex = graphicsQueueFamilyIndex;
        Capabilities = capabilities;
        _destroyDevice = destroyDevice;
        _getDeviceProcAddress = getDeviceProcAddress;
    }

    public VkDevice Handle => _handle;

    public VkQueue GraphicsQueue { get; }

    public uint GraphicsQueueFamilyIndex { get; }

    public VulkanDeviceCapabilities Capabilities { get; }

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

        DeviceFeatureSupport support = QueryFeatureSupport(
            loader,
            instance,
            physicalDevice);
        VulkanDescriptorBindingModel descriptorModel =
            support.DescriptorHeap
                ? VulkanDescriptorBindingModel.DescriptorHeap
                : support.DescriptorBuffer && support.BufferDeviceAddress
                    ? VulkanDescriptorBindingModel.DescriptorBuffer
                    : VulkanDescriptorBindingModel.DescriptorSets;
        VulkanDeviceCapabilities capabilities = new(
            support.DynamicRenderingLocalRead,
            support.DescriptorBuffer,
            support.DescriptorHeap,
            support.BufferDeviceAddress,
            descriptorModel);

        ReadOnlySpan<byte> swapchainName = "VK_KHR_swapchain\0"u8;
        ReadOnlySpan<byte> descriptorBufferName = "VK_EXT_descriptor_buffer\0"u8;
        ReadOnlySpan<byte> descriptorHeapName = "VK_EXT_descriptor_heap\0"u8;
        fixed (byte* swapchainNamePointer = swapchainName)
        fixed (byte* descriptorBufferNamePointer = descriptorBufferName)
        fixed (byte* descriptorHeapNamePointer = descriptorHeapName)
        {
            byte** extensionNames = stackalloc byte*[2];
            uint extensionCount = 0;
            if (enableSwapchain)
            {
                if (!physicalDevice.Extensions.Contains("VK_KHR_swapchain"))
                {
                    throw new NotSupportedException(
                        "The physical device does not support VK_KHR_swapchain.");
                }

                extensionNames[extensionCount++] = swapchainNamePointer;
            }

            if (descriptorModel == VulkanDescriptorBindingModel.DescriptorHeap)
            {
                extensionNames[extensionCount++] = descriptorHeapNamePointer;
            }
            else if (descriptorModel == VulkanDescriptorBindingModel.DescriptorBuffer)
            {
                extensionNames[extensionCount++] = descriptorBufferNamePointer;
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
                TimelineSemaphore = 1
            };
            VkPhysicalDeviceDynamicRenderingLocalReadFeatures localReadFeatures = new()
            {
                SType = VkStructureType.PhysicalDeviceDynamicRenderingLocalReadFeatures,
                DynamicRenderingLocalRead = support.DynamicRenderingLocalRead ? 1u : 0u
            };
            VkPhysicalDeviceBufferDeviceAddressFeatures bufferAddressFeatures = new()
            {
                SType = VkStructureType.PhysicalDeviceBufferDeviceAddressFeatures,
                BufferDeviceAddress = descriptorModel
                    == VulkanDescriptorBindingModel.DescriptorBuffer
                    ? 1u
                    : 0u
            };
            VkPhysicalDeviceDescriptorBufferFeaturesExt descriptorBufferFeatures = new()
            {
                SType = VkStructureType.PhysicalDeviceDescriptorBufferFeaturesExt,
                DescriptorBuffer = descriptorModel
                    == VulkanDescriptorBindingModel.DescriptorBuffer
                    ? 1u
                    : 0u
            };
            VkPhysicalDeviceDescriptorHeapFeaturesExt descriptorHeapFeatures = new()
            {
                SType = VkStructureType.PhysicalDeviceDescriptorHeapFeaturesExt,
                DescriptorHeap = descriptorModel
                    == VulkanDescriptorBindingModel.DescriptorHeap
                    ? 1u
                    : 0u
            };

            void* featureChain = &features13;
            timelineFeatures.PNext = featureChain;
            featureChain = &timelineFeatures;
            if (support.DynamicRenderingLocalRead)
            {
                localReadFeatures.PNext = featureChain;
                featureChain = &localReadFeatures;
            }

            if (descriptorModel == VulkanDescriptorBindingModel.DescriptorBuffer)
            {
                bufferAddressFeatures.PNext = featureChain;
                descriptorBufferFeatures.PNext = &bufferAddressFeatures;
                featureChain = &descriptorBufferFeatures;
            }
            else if (descriptorModel == VulkanDescriptorBindingModel.DescriptorHeap)
            {
                descriptorHeapFeatures.PNext = featureChain;
                featureChain = &descriptorHeapFeatures;
            }

            createInfo.PNext = featureChain;

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
                capabilities,
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

    private static DeviceFeatureSupport QueryFeatureSupport(
        VulkanLoader loader,
        VkInstance instance,
        VulkanPhysicalDeviceInfo physicalDevice)
    {
        nint proc = loader.GetInstanceProcAddress(
            instance,
            "vkGetPhysicalDeviceFeatures2\0"u8);
        if (proc == 0)
        {
            throw new MissingMethodException(
                "The Vulkan instance does not expose vkGetPhysicalDeviceFeatures2.");
        }

        var getFeatures =
            (delegate* unmanaged<VkPhysicalDevice, VkPhysicalDeviceFeatures2*, void>)proc;
        VkPhysicalDeviceDynamicRenderingLocalReadFeatures localRead = new()
        {
            SType = VkStructureType.PhysicalDeviceDynamicRenderingLocalReadFeatures
        };
        VkPhysicalDeviceBufferDeviceAddressFeatures bufferAddress = new()
        {
            SType = VkStructureType.PhysicalDeviceBufferDeviceAddressFeatures
        };
        VkPhysicalDeviceDescriptorBufferFeaturesExt descriptorBuffer = new()
        {
            SType = VkStructureType.PhysicalDeviceDescriptorBufferFeaturesExt
        };
        VkPhysicalDeviceDescriptorHeapFeaturesExt descriptorHeap = new()
        {
            SType = VkStructureType.PhysicalDeviceDescriptorHeapFeaturesExt
        };

        void* featureChain = &localRead;
        bufferAddress.PNext = featureChain;
        featureChain = &bufferAddress;
        bool supportsDescriptorBuffer = physicalDevice.Extensions.Contains(
            "VK_EXT_descriptor_buffer");
        if (supportsDescriptorBuffer)
        {
            descriptorBuffer.PNext = featureChain;
            featureChain = &descriptorBuffer;
        }

        bool supportsDescriptorHeap = physicalDevice.Extensions.Contains(
            "VK_EXT_descriptor_heap");
        if (supportsDescriptorHeap)
        {
            descriptorHeap.PNext = featureChain;
            featureChain = &descriptorHeap;
        }

        VkPhysicalDeviceFeatures2 features = new()
        {
            SType = VkStructureType.PhysicalDeviceFeatures2,
            PNext = featureChain
        };
        getFeatures(physicalDevice.Handle, &features);

        return new(
            localRead.DynamicRenderingLocalRead != 0,
            supportsDescriptorBuffer && descriptorBuffer.DescriptorBuffer != 0,
            supportsDescriptorHeap && descriptorHeap.DescriptorHeap != 0,
            bufferAddress.BufferDeviceAddress != 0);
    }

    private readonly record struct DeviceFeatureSupport(
        bool DynamicRenderingLocalRead,
        bool DescriptorBuffer,
        bool DescriptorHeap,
        bool BufferDeviceAddress);
}
