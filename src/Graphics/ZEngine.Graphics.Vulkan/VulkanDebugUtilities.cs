using System.Text;
using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

internal sealed unsafe class VulkanDebugUtilities
{
    private readonly VulkanDevice _device;
    private readonly delegate* unmanaged<VkDevice, VkDebugUtilsObjectNameInfoExt*, VkResult>
        _setObjectName;
    private readonly delegate* unmanaged<VkCommandBuffer, VkDebugUtilsLabelExt*, void>
        _beginLabel;
    private readonly delegate* unmanaged<VkCommandBuffer, void>
        _endLabel;

    public VulkanDebugUtilities(VulkanDevice device)
    {
        _device = device;
        _setObjectName =
            (delegate* unmanaged<VkDevice, VkDebugUtilsObjectNameInfoExt*, VkResult>)device.TryGetProcAddress(
                "vkSetDebugUtilsObjectNameEXT\0"u8);
        _beginLabel =
            (delegate* unmanaged<VkCommandBuffer, VkDebugUtilsLabelExt*, void>)device.TryGetProcAddress(
                "vkCmdBeginDebugUtilsLabelEXT\0"u8);
        _endLabel =
            (delegate* unmanaged<VkCommandBuffer, void>)device.TryGetProcAddress(
                "vkCmdEndDebugUtilsLabelEXT\0"u8);
    }

    public bool IsEnabled =>
        _setObjectName != null
        && _beginLabel != null
        && _endLabel != null;

    public byte[] EncodeLabel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Encoding.UTF8.GetBytes(name + '\0');
    }

    public void NameObject(
        VkObjectType objectType,
        ulong handle,
        string name)
    {
        if (!IsEnabled || handle == 0)
        {
            return;
        }

        byte[] encoded = EncodeLabel(name);
        fixed (byte* pointer = encoded)
        {
            VkDebugUtilsObjectNameInfoExt nameInfo = new()
            {
                SType = VkStructureType.DebugUtilsObjectNameInfoExt,
                ObjectType = objectType,
                ObjectHandle = handle,
                PObjectName = pointer
            };
            VulkanException.ThrowIfFailed(
                _setObjectName(_device.Handle, &nameInfo),
                "vkSetDebugUtilsObjectNameEXT");
        }
    }

    public void BeginLabel(
        VkCommandBuffer commandBuffer,
        ReadOnlySpan<byte> encodedName)
    {
        if (!IsEnabled)
        {
            return;
        }

        fixed (byte* pointer = encodedName)
        {
            VkDebugUtilsLabelExt label = new()
            {
                SType = VkStructureType.DebugUtilsLabelExt,
                PLabelName = pointer
            };
            label.Color[0] = 0.16f;
            label.Color[1] = 0.55f;
            label.Color[2] = 0.95f;
            label.Color[3] = 1;
            _beginLabel(commandBuffer, &label);
        }
    }

    public void EndLabel(VkCommandBuffer commandBuffer)
    {
        if (IsEnabled)
        {
            _endLabel(commandBuffer);
        }
    }
}
