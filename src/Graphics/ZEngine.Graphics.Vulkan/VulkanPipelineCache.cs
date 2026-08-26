using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public sealed unsafe class VulkanPipelineCache : IDisposable
{
    private readonly VulkanDevice _device;
    private readonly delegate* unmanaged<VkDevice, VkPipelineCache, void*, void>
        _destroyPipelineCache;
    private readonly delegate* unmanaged<VkDevice, VkPipelineCache, nuint*, void*, VkResult>
        _getPipelineCacheData;
    private VkPipelineCache _handle;

    public VulkanPipelineCache(
        VulkanDevice device,
        ReadOnlySpan<byte> initialData = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        var createPipelineCache =
            (delegate* unmanaged<VkDevice, VkPipelineCacheCreateInfo*, void*, VkPipelineCache*, VkResult>)device.GetProcAddress(
                "vkCreatePipelineCache\0"u8);
        _destroyPipelineCache =
            (delegate* unmanaged<VkDevice, VkPipelineCache, void*, void>)device.GetProcAddress(
                "vkDestroyPipelineCache\0"u8);
        _getPipelineCacheData =
            (delegate* unmanaged<VkDevice, VkPipelineCache, nuint*, void*, VkResult>)device.GetProcAddress(
                "vkGetPipelineCacheData\0"u8);

        fixed (byte* initialPointer = initialData)
        {
            VkPipelineCacheCreateInfo createInfo = new()
            {
                SType = VkStructureType.PipelineCacheCreateInfo,
                InitialDataSize = (nuint)initialData.Length,
                PInitialData = initialData.IsEmpty ? null : initialPointer
            };
            VkPipelineCache handle = default;
            VulkanException.ThrowIfFailed(
                createPipelineCache(
                    device.Handle,
                    &createInfo,
                    null,
                    &handle),
                "vkCreatePipelineCache");
            _handle = handle;
        }
    }

    public VkPipelineCache Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsNull, this);
            return _handle;
        }
    }

    public byte[] Snapshot()
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        nuint size = 0;
        VulkanException.ThrowIfFailed(
            _getPipelineCacheData(
                _device.Handle,
                _handle,
                &size,
                null),
            "vkGetPipelineCacheData(size)");
        byte[] result = new byte[checked((int)size)];
        fixed (byte* pointer = result)
        {
            VulkanException.ThrowIfFailedOrIncomplete(
                _getPipelineCacheData(
                    _device.Handle,
                    _handle,
                    &size,
                    pointer),
                "vkGetPipelineCacheData(data)");
        }

        return result.AsSpan(0, checked((int)size)).ToArray();
    }

    public void Dispose()
    {
        VkPipelineCache handle = _handle;
        if (!handle.IsNull)
        {
            _handle = default;
            _destroyPipelineCache(_device.Handle, handle, null);
        }
    }
}
