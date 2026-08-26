using System.Runtime.InteropServices;

namespace ZEngine.Vulkan.Raw;

public enum VkObjectType : int
{
    Unknown = 0,
    Instance = 1,
    PhysicalDevice = 2,
    Device = 3,
    Queue = 4,
    Semaphore = 5,
    CommandBuffer = 6,
    Fence = 7,
    DeviceMemory = 8,
    Buffer = 9,
    Image = 10,
    BufferView = 13,
    ImageView = 14,
    ShaderModule = 15,
    PipelineCache = 16,
    PipelineLayout = 17,
    Pipeline = 19,
    CommandPool = 25,
    SwapchainKhr = 1000001000
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDebugUtilsObjectNameInfoExt
{
    public VkStructureType SType;
    public void* PNext;
    public VkObjectType ObjectType;
    public ulong ObjectHandle;
    public byte* PObjectName;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDebugUtilsLabelExt
{
    public VkStructureType SType;
    public void* PNext;
    public byte* PLabelName;
    public fixed float Color[4];
}
