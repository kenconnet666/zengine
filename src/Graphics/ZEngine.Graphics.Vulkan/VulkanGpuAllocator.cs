using System.Numerics;
using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public sealed unsafe class VulkanGpuAllocator : IGpuAllocator, IDisposable
{
    private const ulong DefaultDeviceBlockSize = 64 * 1024 * 1024;
    private const ulong DefaultHostBlockSize = 16 * 1024 * 1024;
    private readonly VulkanDevice _device;
    private readonly VkPhysicalDeviceMemoryProperties _memoryProperties;
    private readonly List<MemoryBlock> _blocks = [];
    private readonly delegate* unmanaged<VkDevice, VkMemoryAllocateInfo*, void*, VkDeviceMemory*, VkResult>
        _allocateMemory;
    private readonly delegate* unmanaged<VkDevice, VkDeviceMemory, void*, void>
        _freeMemory;
    private readonly delegate* unmanaged<VkDevice, VkDeviceMemory, ulong, ulong, uint, void**, VkResult>
        _mapMemory;
    private readonly delegate* unmanaged<VkDevice, VkDeviceMemory, void>
        _unmapMemory;
    private bool _disposed;

    public VulkanGpuAllocator(
        VulkanDevice device,
        in VkPhysicalDeviceMemoryProperties memoryProperties)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _memoryProperties = memoryProperties;
        _allocateMemory =
            (delegate* unmanaged<VkDevice, VkMemoryAllocateInfo*, void*, VkDeviceMemory*, VkResult>)device.GetProcAddress(
                "vkAllocateMemory\0"u8);
        _freeMemory =
            (delegate* unmanaged<VkDevice, VkDeviceMemory, void*, void>)device.GetProcAddress(
                "vkFreeMemory\0"u8);
        _mapMemory =
            (delegate* unmanaged<VkDevice, VkDeviceMemory, ulong, ulong, uint, void**, VkResult>)device.GetProcAddress(
                "vkMapMemory\0"u8);
        _unmapMemory =
            (delegate* unmanaged<VkDevice, VkDeviceMemory, void>)device.GetProcAddress(
                "vkUnmapMemory\0"u8);
    }

    public int BlockCount => _blocks.Count;

    public int LiveAllocationCount =>
        _blocks.Sum(block => block.LiveAllocations.Count);

    public ulong ReservedBytes =>
        _blocks.Aggregate(0ul, (total, block) => total + block.Size);

    public GpuAllocation Allocate(in GpuAllocationRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfZero(request.Size);
        ArgumentOutOfRangeException.ThrowIfZero(request.Alignment);

        uint memoryTypeIndex = SelectMemoryType(
            request.CompatibleMemoryTypes,
            request.MemoryClass);
        ulong defaultBlockSize = request.MemoryClass == GpuMemoryClass.DeviceLocal
            ? DefaultDeviceBlockSize
            : DefaultHostBlockSize;
        bool dedicated = request.Size > defaultBlockSize / 2;

        if (!dedicated)
        {
            foreach (MemoryBlock candidate in _blocks)
            {
                if (candidate.MemoryTypeIndex == memoryTypeIndex
                    && !candidate.Dedicated
                    && candidate.TryAllocate(
                        request.Size,
                        request.Alignment,
                        out ulong offset))
                {
                    return CreateAllocation(candidate, offset, request.Size);
                }
            }
        }

        ulong blockSize = dedicated
            ? AlignUp(request.Size, request.Alignment)
            : Math.Max(
                defaultBlockSize,
                AlignUp(request.Size, request.Alignment));
        MemoryBlock block = CreateBlock(
            memoryTypeIndex,
            blockSize,
            dedicated);
        _blocks.Add(block);
        if (!block.TryAllocate(
                request.Size,
                request.Alignment,
                out ulong blockOffset))
        {
            throw new InvalidOperationException(
                "A newly allocated Vulkan memory block could not satisfy its originating request.");
        }

        return CreateAllocation(block, blockOffset, request.Size);
    }

    public void Release(in GpuAllocation allocation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        nint backendHandle = allocation.BackendHandle;
        MemoryBlock block = _blocks.Find(candidate =>
                candidate.Memory.ToBackendHandle() == backendHandle)
            ?? throw new InvalidOperationException(
                "The GPU allocation is not owned by this Vulkan allocator.");
        block.Release(allocation.Offset, allocation.Size);
        if (block.Dedicated && block.LiveAllocations.Count == 0)
        {
            DestroyBlock(block);
            _blocks.Remove(block);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        for (int index = _blocks.Count - 1; index >= 0; index--)
        {
            DestroyBlock(_blocks[index]);
        }

        _blocks.Clear();
    }

    private MemoryBlock CreateBlock(
        uint memoryTypeIndex,
        ulong size,
        bool dedicated)
    {
        VkMemoryAllocateInfo allocateInfo = new()
        {
            SType = VkStructureType.MemoryAllocateInfo,
            AllocationSize = size,
            MemoryTypeIndex = memoryTypeIndex
        };
        VkDeviceMemory memory = default;
        VulkanException.ThrowIfFailed(
            _allocateMemory(
                _device.Handle,
                &allocateInfo,
                null,
                &memory),
            "vkAllocateMemory(block)");

        VkMemoryPropertyFlags properties =
            _memoryProperties.MemoryTypes[checked((int)memoryTypeIndex)].PropertyFlags;
        nint mappedAddress = 0;
        if ((properties & VkMemoryPropertyFlags.HostVisible) != 0)
        {
            void* mapped = null;
            try
            {
                VulkanException.ThrowIfFailed(
                    _mapMemory(
                        _device.Handle,
                        memory,
                        0,
                        size,
                        0,
                        &mapped),
                    "vkMapMemory(block)");
                mappedAddress = (nint)mapped;
            }
            catch
            {
                _freeMemory(_device.Handle, memory, null);
                throw;
            }
        }

        return new(
            memory,
            memoryTypeIndex,
            size,
            properties,
            mappedAddress,
            dedicated);
    }

    private void DestroyBlock(MemoryBlock block)
    {
        if (block.MappedAddress != 0)
        {
            _unmapMemory(_device.Handle, block.Memory);
        }

        _freeMemory(_device.Handle, block.Memory, null);
    }

    private GpuAllocation CreateAllocation(
        MemoryBlock block,
        ulong offset,
        ulong size) =>
        new(
            offset,
            size,
            block.MemoryTypeIndex,
            block.Memory.ToBackendHandle(),
            block.MappedAddress == 0
                ? 0
                : block.MappedAddress + checked((nint)offset));

    private uint SelectMemoryType(
        uint compatibleMemoryTypes,
        GpuMemoryClass memoryClass)
    {
        VkMemoryPropertyFlags required = memoryClass switch
        {
            GpuMemoryClass.DeviceLocal => VkMemoryPropertyFlags.DeviceLocal,
            GpuMemoryClass.Upload or GpuMemoryClass.Readback =>
                VkMemoryPropertyFlags.HostVisible
                | VkMemoryPropertyFlags.HostCoherent,
            _ => throw new ArgumentOutOfRangeException(nameof(memoryClass))
        };
        VkMemoryPropertyFlags preferred = memoryClass switch
        {
            GpuMemoryClass.DeviceLocal => VkMemoryPropertyFlags.DeviceLocal,
            GpuMemoryClass.Upload => VkMemoryPropertyFlags.HostCoherent,
            GpuMemoryClass.Readback =>
                VkMemoryPropertyFlags.HostCoherent
                | VkMemoryPropertyFlags.HostCached,
            _ => VkMemoryPropertyFlags.None
        };

        int bestScore = -1;
        uint bestIndex = uint.MaxValue;
        for (uint index = 0; index < _memoryProperties.MemoryTypeCount; index++)
        {
            uint bit = 1u << checked((int)index);
            if ((compatibleMemoryTypes & bit) == 0)
            {
                continue;
            }

            VkMemoryPropertyFlags flags =
                _memoryProperties.MemoryTypes[checked((int)index)].PropertyFlags;
            if ((flags & required) != required)
            {
                continue;
            }

            int score = BitOperations.PopCount((uint)(flags & preferred));
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        return bestIndex != uint.MaxValue
            ? bestIndex
            : throw new NotSupportedException(
                $"No Vulkan memory type satisfies {memoryClass} for mask 0x{compatibleMemoryTypes:X8}.");
    }

    private static ulong AlignUp(ulong value, ulong alignment)
    {
        ulong remainder = value % alignment;
        return remainder == 0
            ? value
            : checked(value + alignment - remainder);
    }

    private sealed class MemoryBlock
    {
        private readonly List<FreeRange> _freeRanges;

        public MemoryBlock(
            VkDeviceMemory memory,
            uint memoryTypeIndex,
            ulong size,
            VkMemoryPropertyFlags properties,
            nint mappedAddress,
            bool dedicated)
        {
            Memory = memory;
            MemoryTypeIndex = memoryTypeIndex;
            Size = size;
            Properties = properties;
            MappedAddress = mappedAddress;
            Dedicated = dedicated;
            _freeRanges = [new(0, size)];
        }

        public VkDeviceMemory Memory { get; }

        public uint MemoryTypeIndex { get; }

        public ulong Size { get; }

        public VkMemoryPropertyFlags Properties { get; }

        public nint MappedAddress { get; }

        public bool Dedicated { get; }

        public Dictionary<ulong, ulong> LiveAllocations { get; } = [];

        public bool TryAllocate(
            ulong size,
            ulong alignment,
            out ulong offset)
        {
            for (int index = 0; index < _freeRanges.Count; index++)
            {
                FreeRange range = _freeRanges[index];
                ulong aligned = AlignUp(range.Offset, alignment);
                ulong padding = aligned - range.Offset;
                if (padding > range.Size || size > range.Size - padding)
                {
                    continue;
                }

                _freeRanges.RemoveAt(index);
                if (padding > 0)
                {
                    _freeRanges.Insert(index++, new(range.Offset, padding));
                }

                ulong usedEnd = checked(aligned + size);
                ulong rangeEnd = checked(range.Offset + range.Size);
                if (usedEnd < rangeEnd)
                {
                    _freeRanges.Insert(index, new(usedEnd, rangeEnd - usedEnd));
                }

                LiveAllocations.Add(aligned, size);
                offset = aligned;
                return true;
            }

            offset = 0;
            return false;
        }

        public void Release(ulong offset, ulong size)
        {
            if (!LiveAllocations.Remove(offset, out ulong liveSize)
                || liveSize != size)
            {
                throw new InvalidOperationException(
                    "The Vulkan suballocation is stale or its size does not match the live allocation.");
            }

            _freeRanges.Add(new(offset, size));
            _freeRanges.Sort(static (left, right) =>
                left.Offset.CompareTo(right.Offset));
            for (int index = _freeRanges.Count - 1; index > 0; index--)
            {
                FreeRange previous = _freeRanges[index - 1];
                FreeRange current = _freeRanges[index];
                if (previous.Offset + previous.Size != current.Offset)
                {
                    continue;
                }

                _freeRanges[index - 1] = new(
                    previous.Offset,
                    previous.Size + current.Size);
                _freeRanges.RemoveAt(index);
            }
        }
    }

    private readonly record struct FreeRange(ulong Offset, ulong Size);
}
