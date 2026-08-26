using System.Runtime.InteropServices;

namespace ZEngine.Vulkan.Raw;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPipelineCacheCreateInfo
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public nuint InitialDataSize;
    public void* PInitialData;
}
