using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public interface IVulkanOverlayFactory
{
    IVulkanOverlay Create(VulkanDevice device, VkFormat colorFormat);
}

public interface IVulkanOverlay : IDisposable
{
    bool HasContent { get; }

    void Draw(VkCommandBuffer commandBuffer, VkExtent2D extent);
}
