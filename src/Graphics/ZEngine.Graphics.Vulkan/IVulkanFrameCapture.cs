using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public interface IVulkanFrameCaptureFactory
{
    IVulkanFrameCapture Create(
        VulkanDevice device,
        VulkanGpuAllocator allocator,
        VkExtent2D extent,
        VkFormat format);
}

public interface IVulkanFrameCapture : IDisposable
{
    void Capture(
        VkCommandBuffer commandBuffer,
        VkImage image,
        VkExtent2D extent);

    void OnSubmitted(ulong timelineValue);

    byte[] ReadPng();
}
