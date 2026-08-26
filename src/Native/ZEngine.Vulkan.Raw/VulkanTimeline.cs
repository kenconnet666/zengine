using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

public sealed unsafe class VulkanTimeline : IDisposable
{
    private readonly VulkanDevice _device;
    private readonly delegate* unmanaged<VkDevice, VkSemaphore, void*, void>
        _destroySemaphore;
    private readonly delegate* unmanaged<VkDevice, VkSemaphore, ulong*, VkResult>
        _getCounterValue;
    private readonly delegate* unmanaged<VkDevice, VkSemaphoreWaitInfo*, ulong, VkResult>
        _waitSemaphores;
    private readonly delegate* unmanaged<VkDevice, VkSemaphoreSignalInfo*, VkResult>
        _signalSemaphore;
    private long _nextSignalValue;
    private VkSemaphore _handle;

    internal VulkanTimeline(VulkanDevice device, ulong initialValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            initialValue,
            (ulong)long.MaxValue);
        _device = device;
        _nextSignalValue = checked((long)initialValue);

        var createSemaphore =
            (delegate* unmanaged<VkDevice, VkSemaphoreCreateInfo*, void*, VkSemaphore*, VkResult>)device.GetProcAddress(
                "vkCreateSemaphore\0"u8);
        _destroySemaphore =
            (delegate* unmanaged<VkDevice, VkSemaphore, void*, void>)device.GetProcAddress(
                "vkDestroySemaphore\0"u8);
        _getCounterValue =
            (delegate* unmanaged<VkDevice, VkSemaphore, ulong*, VkResult>)device.GetProcAddress(
                "vkGetSemaphoreCounterValue\0"u8);
        _waitSemaphores =
            (delegate* unmanaged<VkDevice, VkSemaphoreWaitInfo*, ulong, VkResult>)device.GetProcAddress(
                "vkWaitSemaphores\0"u8);
        _signalSemaphore =
            (delegate* unmanaged<VkDevice, VkSemaphoreSignalInfo*, VkResult>)device.GetProcAddress(
                "vkSignalSemaphore\0"u8);

        VkSemaphoreTypeCreateInfo typeInfo = new()
        {
            SType = VkStructureType.SemaphoreTypeCreateInfo,
            SemaphoreType = VkSemaphoreType.Timeline,
            InitialValue = initialValue
        };
        VkSemaphoreCreateInfo createInfo = new()
        {
            SType = VkStructureType.SemaphoreCreateInfo,
            PNext = &typeInfo
        };
        VkSemaphore handle = default;
        VulkanException.ThrowIfFailed(
            createSemaphore(
                device.Handle,
                &createInfo,
                null,
                &handle),
            "vkCreateSemaphore(timeline)");
        _handle = handle;
    }

    public VkSemaphore Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsNull, this);
            return _handle;
        }
    }

    public ulong CompletedValue
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsNull, this);
            ulong value = 0;
            VulkanException.ThrowIfFailed(
                _getCounterValue(_device.Handle, _handle, &value),
                "vkGetSemaphoreCounterValue");
            return value;
        }
    }

    public ulong ReserveSignalValue() =>
        checked((ulong)Interlocked.Increment(ref _nextSignalValue));

    public void Signal(ulong value)
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        VkSemaphoreSignalInfo signalInfo = new()
        {
            SType = VkStructureType.SemaphoreSignalInfo,
            Semaphore = _handle,
            Value = value
        };
        VulkanException.ThrowIfFailed(
            _signalSemaphore(_device.Handle, &signalInfo),
            "vkSignalSemaphore");
        AdvanceReservation(value);
    }

    public bool Wait(ulong value, ulong timeoutNanoseconds = ulong.MaxValue)
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        VkSemaphore semaphore = _handle;
        VkSemaphoreWaitInfo waitInfo = new()
        {
            SType = VkStructureType.SemaphoreWaitInfo,
            SemaphoreCount = 1,
            PSemaphores = &semaphore,
            PValues = &value
        };
        VkResult result = _waitSemaphores(
            _device.Handle,
            &waitInfo,
            timeoutNanoseconds);
        if (result == VkResult.Timeout)
        {
            return false;
        }

        VulkanException.ThrowIfFailed(result, "vkWaitSemaphores");
        return true;
    }

    public void Dispose()
    {
        VkSemaphore handle = _handle;
        if (!handle.IsNull)
        {
            _handle = default;
            _destroySemaphore(_device.Handle, handle, null);
        }
    }

    private void AdvanceReservation(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            value,
            (ulong)long.MaxValue);
        long target = checked((long)value);
        long current = Volatile.Read(ref _nextSignalValue);
        while (current < target)
        {
            long observed = Interlocked.CompareExchange(
                ref _nextSignalValue,
                target,
                current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }
}
