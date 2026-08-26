namespace ZEngine.Vulkan.Raw;

public sealed class VulkanException(
    VkResult result,
    string operation)
    : Exception($"{operation} failed with {result} ({(int)result}).")
{
    public VkResult Result { get; } = result;

    public string Operation { get; } = operation;

    public static void ThrowIfFailed(
        VkResult result,
        string operation)
    {
        if (result != VkResult.Success)
        {
            throw new VulkanException(result, operation);
        }
    }

    public static void ThrowIfFailedOrIncomplete(
        VkResult result,
        string operation)
    {
        if (result is not (VkResult.Success or VkResult.Incomplete))
        {
            throw new VulkanException(result, operation);
        }
    }
}
