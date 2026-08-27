using System.Runtime.InteropServices;
using ZEngine.Graphics.Vulkan;
using ZEngine.Testing;
using ZEngine.Vulkan.Raw;
using Xunit;

namespace ZEngine.Graphics.Vulkan.Tests;

public sealed class VulkanAllocatorTests
{
    [Fact]
    public void SuballocatesMapsWritesAndMergesBufferMemory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using VulkanLoader loader = VulkanLoader.OpenSystem();
        using VulkanInstance instance = VulkanInstance.Create(
            loader,
            VulkanInstanceOptions.Validation);
        VulkanPhysicalDeviceInfo physicalDevice = VulkanTestDevice.Select(instance);
        using VulkanDevice device = instance.CreateGraphicsDevice(physicalDevice);
        using VulkanGpuAllocator allocator = new(
            device,
            physicalDevice.MemoryProperties);
        using VulkanBuffer first = VulkanBuffer.Create(
            device,
            allocator,
            4096,
            VkBufferUsageFlags.TransferSource,
            GpuMemoryClass.Upload);
        using VulkanBuffer second = VulkanBuffer.Create(
            device,
            allocator,
            4096,
            VkBufferUsageFlags.TransferSource,
            GpuMemoryClass.Upload);
        using VulkanBuffer deviceLocal = VulkanBuffer.Create(
            device,
            allocator,
            4096,
            VkBufferUsageFlags.TransferDestination
            | VkBufferUsageFlags.VertexBuffer,
            GpuMemoryClass.DeviceLocal);

        Assert.Equal(first.Allocation.BackendHandle, second.Allocation.BackendHandle);
        Assert.NotEqual(first.Allocation.Offset, second.Allocation.Offset);
        Assert.Equal(0, deviceLocal.Allocation.MappedAddress);
        Assert.Equal(2, allocator.BlockCount);
        Assert.Equal(3, allocator.LiveAllocationCount);

        int[] values = [1, 2, 3, 5, 8, 13];
        first.Write<int>(values);
        Assert.Equal(
            values,
            MemoryMarshal.Cast<byte, int>(
                first.GetMappedBytes()[..(values.Length * sizeof(int))])
                .ToArray());

        nint sharedBlock = first.Allocation.BackendHandle;
        first.Dispose();
        second.Dispose();
        using VulkanBuffer recycled = VulkanBuffer.Create(
            device,
            allocator,
            8192,
            VkBufferUsageFlags.TransferSource,
            GpuMemoryClass.Upload);
        Assert.Equal(sharedBlock, recycled.Allocation.BackendHandle);
        Assert.Equal(0ul, recycled.Allocation.Offset);

        recycled.Dispose();
        deviceLocal.Dispose();
        Assert.Equal(0, allocator.LiveAllocationCount);

        device.WaitIdle();
        Assert.DoesNotContain(
            instance.ValidationMessages,
            message =>
                (message.Severity & VkDebugUtilsMessageSeverityFlagsExt.Error) != 0);
    }
}
