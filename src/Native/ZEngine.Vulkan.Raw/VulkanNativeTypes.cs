using System.Runtime.InteropServices;

namespace ZEngine.Vulkan.Raw;

public enum VkResult : int
{
    Success = 0,
    NotReady = 1,
    Timeout = 2,
    Incomplete = 5,
    SuboptimalKhr = 1000001003,
    ErrorOutOfHostMemory = -1,
    ErrorOutOfDeviceMemory = -2,
    ErrorInitializationFailed = -3,
    ErrorLayerNotPresent = -6,
    ErrorExtensionNotPresent = -7,
    ErrorFeatureNotPresent = -8,
    ErrorIncompatibleDriver = -9,
    ErrorOutOfDateKhr = -1000001004
}

public enum VkStructureType : int
{
    ApplicationInfo = 0,
    InstanceCreateInfo = 1,
    DeviceQueueCreateInfo = 2,
    DeviceCreateInfo = 3,
    MemoryAllocateInfo = 5,
    BufferCreateInfo = 12,
    PhysicalDeviceVulkan13Features = 53,
    FenceCreateInfo = 8,
    SemaphoreCreateInfo = 9,
    ImageViewCreateInfo = 15,
    ShaderModuleCreateInfo = 16,
    PipelineShaderStageCreateInfo = 18,
    PipelineVertexInputStateCreateInfo = 19,
    PipelineInputAssemblyStateCreateInfo = 20,
    PipelineViewportStateCreateInfo = 22,
    PipelineRasterizationStateCreateInfo = 23,
    PipelineMultisampleStateCreateInfo = 24,
    PipelineColorBlendStateCreateInfo = 26,
    PipelineDynamicStateCreateInfo = 27,
    GraphicsPipelineCreateInfo = 28,
    PipelineLayoutCreateInfo = 30,
    CommandPoolCreateInfo = 39,
    CommandBufferAllocateInfo = 40,
    CommandBufferBeginInfo = 42,
    SwapchainCreateInfoKhr = 1000001000,
    PresentInfoKhr = 1000001001,
    Win32SurfaceCreateInfoKhr = 1000009000,
    ImageMemoryBarrier2 = 1000314002,
    DependencyInfo = 1000314003,
    SemaphoreSubmitInfo = 1000314005,
    CommandBufferSubmitInfo = 1000314006,
    SubmitInfo2 = 1000314004,
    RenderingInfo = 1000044000,
    RenderingAttachmentInfo = 1000044001,
    PipelineRenderingCreateInfo = 1000044002,
    DebugUtilsMessengerCreateInfoExt = 1000128004,
    PhysicalDeviceTimelineSemaphoreFeatures = 1000207000,
    SemaphoreTypeCreateInfo = 1000207002,
    SemaphoreWaitInfo = 1000207004,
    SemaphoreSignalInfo = 1000207005,
    PhysicalDeviceFeatures2 = 1000059000,
    PhysicalDeviceBufferDeviceAddressFeatures = 1000257000,
    PhysicalDeviceDynamicRenderingLocalReadFeatures = 1000232000,
    PhysicalDeviceDescriptorBufferFeaturesExt = 1000316002,
    PhysicalDeviceDescriptorHeapFeaturesExt = 1000135009
}

public enum VkPhysicalDeviceType : int
{
    Other = 0,
    IntegratedGpu = 1,
    DiscreteGpu = 2,
    VirtualGpu = 3,
    Cpu = 4
}

public enum VkFormat : int
{
    Undefined = 0,
    B8G8R8A8Unorm = 44,
    B8G8R8A8Srgb = 50
}

public enum VkColorSpaceKhr : int
{
    SrgbNonlinear = 0
}

public enum VkPresentModeKhr : int
{
    Immediate = 0,
    Mailbox = 1,
    Fifo = 2,
    FifoRelaxed = 3
}

public enum VkSharingMode : int
{
    Exclusive = 0,
    Concurrent = 1
}

[Flags]
public enum VkQueueFlags : uint
{
    Graphics = 0x00000001,
    Compute = 0x00000002,
    Transfer = 0x00000004,
    SparseBinding = 0x00000008
}

[Flags]
public enum VkImageUsageFlags : uint
{
    TransferSource = 0x00000001,
    TransferDestination = 0x00000002,
    Sampled = 0x00000004,
    Storage = 0x00000008,
    ColorAttachment = 0x00000010,
    DepthStencilAttachment = 0x00000020
}

[Flags]
public enum VkSurfaceTransformFlagsKhr : uint
{
    Identity = 0x00000001,
    Rotate90 = 0x00000002,
    Rotate180 = 0x00000004,
    Rotate270 = 0x00000008,
    Inherit = 0x00000100
}

[Flags]
public enum VkCompositeAlphaFlagsKhr : uint
{
    Opaque = 0x00000001,
    PreMultiplied = 0x00000002,
    PostMultiplied = 0x00000004,
    Inherit = 0x00000008
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkApplicationInfo
{
    public VkStructureType SType;
    public void* PNext;
    public byte* PApplicationName;
    public uint ApplicationVersion;
    public byte* PEngineName;
    public uint EngineVersion;
    public uint ApiVersion;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkInstanceCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public VkApplicationInfo* PApplicationInfo;
    public uint EnabledLayerCount;
    public byte** PpEnabledLayerNames;
    public uint EnabledExtensionCount;
    public byte** PpEnabledExtensionNames;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDeviceQueueCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint QueueFamilyIndex;
    public uint QueueCount;
    public float* PQueuePriorities;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDeviceCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public uint QueueCreateInfoCount;
    public VkDeviceQueueCreateInfo* PQueueCreateInfos;
    public uint EnabledLayerCount;
    public byte** PpEnabledLayerNames;
    public uint EnabledExtensionCount;
    public byte** PpEnabledExtensionNames;
    public void* PEnabledFeatures;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceVulkan13Features
{
    public VkStructureType SType;
    public void* PNext;
    public uint RobustImageAccess;
    public uint InlineUniformBlock;
    public uint DescriptorBindingInlineUniformBlockUpdateAfterBind;
    public uint PipelineCreationCacheControl;
    public uint PrivateData;
    public uint ShaderDemoteToHelperInvocation;
    public uint ShaderTerminateInvocation;
    public uint SubgroupSizeControl;
    public uint ComputeFullSubgroups;
    public uint Synchronization2;
    public uint TextureCompressionAstcHdr;
    public uint ShaderZeroInitializeWorkgroupMemory;
    public uint DynamicRendering;
    public uint ShaderIntegerDotProduct;
    public uint Maintenance4;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceTimelineSemaphoreFeatures
{
    public VkStructureType SType;
    public void* PNext;
    public uint TimelineSemaphore;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkExtent3D
{
    public uint Width;
    public uint Height;
    public uint Depth;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkExtent2D
{
    public uint Width;
    public uint Height;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkQueueFamilyProperties
{
    public VkQueueFlags QueueFlags;
    public uint QueueCount;
    public uint TimestampValidBits;
    public VkExtent3D MinImageTransferGranularity;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkExtensionProperties
{
    public fixed byte ExtensionName[256];
    public uint SpecVersion;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDevicePropertiesBuffer
{
    public uint ApiVersion;
    public uint DriverVersion;
    public uint VendorId;
    public uint DeviceId;
    public VkPhysicalDeviceType DeviceType;
    public fixed byte DeviceName[256];
    public fixed byte PipelineCacheUuid[16];
    public fixed byte RemainingProperties[2048];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkWin32SurfaceCreateInfoKhr
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public nint Instance;
    public nint Window;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkSurfaceCapabilitiesKhr
{
    public uint MinImageCount;
    public uint MaxImageCount;
    public VkExtent2D CurrentExtent;
    public VkExtent2D MinImageExtent;
    public VkExtent2D MaxImageExtent;
    public uint MaxImageArrayLayers;
    public VkSurfaceTransformFlagsKhr SupportedTransforms;
    public VkSurfaceTransformFlagsKhr CurrentTransform;
    public VkCompositeAlphaFlagsKhr SupportedCompositeAlpha;
    public VkImageUsageFlags SupportedUsageFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct VkSurfaceFormatKhr
{
    public VkFormat Format;
    public VkColorSpaceKhr ColorSpace;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkSwapchainCreateInfoKhr
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public ZEngine.Vulkan.Raw.Generated.VkSurfaceKHR Surface;
    public uint MinImageCount;
    public VkFormat ImageFormat;
    public VkColorSpaceKhr ImageColorSpace;
    public VkExtent2D ImageExtent;
    public uint ImageArrayLayers;
    public VkImageUsageFlags ImageUsage;
    public VkSharingMode ImageSharingMode;
    public uint QueueFamilyIndexCount;
    public uint* PQueueFamilyIndices;
    public VkSurfaceTransformFlagsKhr PreTransform;
    public VkCompositeAlphaFlagsKhr CompositeAlpha;
    public VkPresentModeKhr PresentMode;
    public uint Clipped;
    public ZEngine.Vulkan.Raw.Generated.VkSwapchainKHR OldSwapchain;
}
