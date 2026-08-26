using System.Runtime.InteropServices;
using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public sealed unsafe class VulkanBuffer : IDisposable
{
    private readonly VulkanDevice _device;
    private readonly VulkanGpuAllocator _allocator;
    private readonly delegate* unmanaged<VkDevice, VkBuffer, void*, void>
        _destroyBuffer;
    private VkBuffer _handle;
    private GpuAllocation _allocation;

    private VulkanBuffer(
        VulkanDevice device,
        VulkanGpuAllocator allocator,
        VkBuffer handle,
        in GpuAllocation allocation,
        ulong size,
        delegate* unmanaged<VkDevice, VkBuffer, void*, void> destroyBuffer)
    {
        _device = device;
        _allocator = allocator;
        _handle = handle;
        _allocation = allocation;
        Size = size;
        _destroyBuffer = destroyBuffer;
    }

    public VkBuffer Handle => _handle;

    public ulong Size { get; }

    public GpuAllocation Allocation => _allocation;

    public static VulkanBuffer Create(
        VulkanDevice device,
        VulkanGpuAllocator allocator,
        ulong size,
        VkBufferUsageFlags usage,
        GpuMemoryClass memoryClass)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(allocator);
        ArgumentOutOfRangeException.ThrowIfZero(size);
        ArgumentOutOfRangeException.ThrowIfEqual(usage, VkBufferUsageFlags.None);

        var createBuffer =
            (delegate* unmanaged<VkDevice, VkBufferCreateInfo*, void*, VkBuffer*, VkResult>)device.GetProcAddress(
                "vkCreateBuffer\0"u8);
        var destroyBuffer =
            (delegate* unmanaged<VkDevice, VkBuffer, void*, void>)device.GetProcAddress(
                "vkDestroyBuffer\0"u8);
        var getRequirements =
            (delegate* unmanaged<VkDevice, VkBuffer, VkMemoryRequirements*, void>)device.GetProcAddress(
                "vkGetBufferMemoryRequirements\0"u8);
        var bindMemory =
            (delegate* unmanaged<VkDevice, VkBuffer, VkDeviceMemory, ulong, VkResult>)device.GetProcAddress(
                "vkBindBufferMemory\0"u8);

        VkBufferCreateInfo createInfo = new()
        {
            SType = VkStructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = VkSharingMode.Exclusive
        };
        VkBuffer handle = default;
        VulkanException.ThrowIfFailed(
            createBuffer(
                device.Handle,
                &createInfo,
                null,
                &handle),
            "vkCreateBuffer");

        GpuAllocation allocation = default;
        try
        {
            VkMemoryRequirements requirements = default;
            getRequirements(device.Handle, handle, &requirements);
            allocation = allocator.Allocate(
                new(
                    requirements.Size,
                    requirements.Alignment,
                    memoryClass,
                    Transient: false,
                    requirements.MemoryTypeBits));
            VulkanException.ThrowIfFailed(
                bindMemory(
                    device.Handle,
                    handle,
                    allocation.BackendHandle.ToDeviceMemory(),
                    allocation.Offset),
                "vkBindBufferMemory");
            return new(
                device,
                allocator,
                handle,
                allocation,
                size,
                destroyBuffer);
        }
        catch
        {
            if (allocation.BackendHandle != 0)
            {
                allocator.Release(in allocation);
            }

            destroyBuffer(device.Handle, handle, null);
            throw;
        }
    }

    public Span<byte> GetMappedBytes()
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        if (_allocation.MappedAddress == 0)
        {
            throw new InvalidOperationException(
                "The Vulkan buffer was not allocated from host-visible memory.");
        }

        return new(
            (void*)_allocation.MappedAddress,
            checked((int)Size));
    }

    public void Write<T>(ReadOnlySpan<T> values)
        where T : unmanaged
    {
        ReadOnlySpan<byte> source = MemoryMarshal.AsBytes(values);
        if ((ulong)source.Length > Size)
        {
            throw new ArgumentException(
                "The source data is larger than the Vulkan buffer.",
                nameof(values));
        }

        source.CopyTo(GetMappedBytes());
    }

    public void Dispose()
    {
        VkBuffer handle = _handle;
        if (!handle.IsNull)
        {
            _handle = default;
            _destroyBuffer(_device.Handle, handle, null);
            GpuAllocation allocation = _allocation;
            _allocation = default;
            _allocator.Release(in allocation);
        }
    }
}
