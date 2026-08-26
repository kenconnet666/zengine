using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public sealed unsafe class VulkanDescriptorHeap : IDisposable
{
    private readonly VulkanBuffer _buffer;
    private readonly delegate* unmanaged<VkCommandBuffer, VkBindHeapInfoExt*, void>
        _cmdBindResourceHeap;

    public VulkanDescriptorHeap(
        VulkanDevice device,
        VulkanGpuAllocator allocator,
        ulong requestedSize = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(allocator);
        VulkanDescriptorHeapProperties properties =
            device.Capabilities.DescriptorHeapProperties
            ?? throw new NotSupportedException(
                "The Vulkan device did not enable VK_EXT_descriptor_heap.");
        ulong alignment = Math.Max(1, properties.ResourceHeapAlignment);
        ulong minimum = Math.Max(
            requestedSize,
            properties.MinResourceHeapReservedRange);
        Size = AlignUp(minimum, alignment);
        if (Size > properties.MaxResourceHeapSize)
        {
            throw new NotSupportedException(
                $"Requested descriptor heap size {Size} exceeds device maximum {properties.MaxResourceHeapSize}.");
        }

        ReservedRangeSize = properties.MinResourceHeapReservedRange;
        FirstUsableOffset = AlignUp(
            ReservedRangeSize,
            Math.Max(1, properties.BufferDescriptorAlignment));
        _buffer = VulkanBuffer.Create(
            device,
            allocator,
            Size,
            VkBufferUsageFlags.DescriptorHeapExt
            | VkBufferUsageFlags.ShaderDeviceAddress,
            GpuMemoryClass.Upload);

        var getBufferDeviceAddress =
            (delegate* unmanaged<VkDevice, VkBufferDeviceAddressInfo*, ulong>)device.GetProcAddress(
                "vkGetBufferDeviceAddress\0"u8);
        VkBufferDeviceAddressInfo addressInfo = new()
        {
            SType = VkStructureType.BufferDeviceAddressInfo,
            Buffer = _buffer.Handle
        };
        DeviceAddress = getBufferDeviceAddress(
            device.Handle,
            &addressInfo);
        if (DeviceAddress == 0)
        {
            _buffer.Dispose();
            throw new InvalidOperationException(
                "The descriptor heap buffer has no device address.");
        }

        _cmdBindResourceHeap =
            (delegate* unmanaged<VkCommandBuffer, VkBindHeapInfoExt*, void>)device.GetProcAddress(
                "vkCmdBindResourceHeapEXT\0"u8);
    }

    public ulong Size { get; }

    public ulong DeviceAddress { get; }

    public ulong ReservedRangeSize { get; }

    public ulong FirstUsableOffset { get; }

    public Span<byte> GetMappedBytes() => _buffer.GetMappedBytes();

    public void Bind(VkCommandBuffer commandBuffer)
    {
        VkBindHeapInfoExt bindInfo = new()
        {
            SType = VkStructureType.BindHeapInfoExt,
            HeapRange = new()
            {
                Address = DeviceAddress,
                Size = Size
            },
            ReservedRangeOffset = 0,
            ReservedRangeSize = ReservedRangeSize
        };
        _cmdBindResourceHeap(commandBuffer, &bindInfo);
    }

    public void Dispose() => _buffer.Dispose();

    private static ulong AlignUp(ulong value, ulong alignment)
    {
        ulong remainder = value % alignment;
        return remainder == 0
            ? value
            : checked(value + alignment - remainder);
    }
}
