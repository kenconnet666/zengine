using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

[Flags]
public enum VkDebugUtilsMessageSeverityFlagsExt : uint
{
    Verbose = 0x00000001,
    Info = 0x00000010,
    Warning = 0x00000100,
    Error = 0x00001000
}

[Flags]
public enum VkDebugUtilsMessageTypeFlagsExt : uint
{
    General = 0x00000001,
    Validation = 0x00000002,
    Performance = 0x00000004,
    DeviceAddressBinding = 0x00000008
}

public sealed record VulkanValidationMessage(
    VkDebugUtilsMessageSeverityFlagsExt Severity,
    VkDebugUtilsMessageTypeFlagsExt Type,
    int Id,
    string? IdName,
    string Message);

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDebugUtilsMessengerCallbackDataExt
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public byte* PMessageIdName;
    public int MessageIdNumber;
    public byte* PMessage;
    public uint QueueLabelCount;
    public void* PQueueLabels;
    public uint CommandBufferLabelCount;
    public void* PCommandBufferLabels;
    public uint ObjectCount;
    public void* PObjects;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDebugUtilsMessengerCreateInfoExt
{
    public VkStructureType SType;
    public void* PNext;
    public uint Flags;
    public VkDebugUtilsMessageSeverityFlagsExt MessageSeverity;
    public VkDebugUtilsMessageTypeFlagsExt MessageType;
    public delegate* unmanaged[Stdcall]<
        VkDebugUtilsMessageSeverityFlagsExt,
        VkDebugUtilsMessageTypeFlagsExt,
        VkDebugUtilsMessengerCallbackDataExt*,
        void*,
        uint> UserCallback;
    public void* UserData;
}

internal sealed unsafe class VulkanValidationCollector : IDisposable
{
    private readonly ConcurrentQueue<VulkanValidationMessage> _messages = new();
    private GCHandle _selfHandle;

    internal VulkanValidationCollector()
    {
        _selfHandle = GCHandle.Alloc(this);
    }

    internal void* UserData => (void*)GCHandle.ToIntPtr(_selfHandle);

    internal IReadOnlyList<VulkanValidationMessage> Snapshot() =>
        _messages.ToArray();

    public void Dispose()
    {
        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    internal static uint Callback(
        VkDebugUtilsMessageSeverityFlagsExt severity,
        VkDebugUtilsMessageTypeFlagsExt type,
        VkDebugUtilsMessengerCallbackDataExt* callbackData,
        void* userData)
    {
        if (userData == null || callbackData == null)
        {
            return 0;
        }

        GCHandle handle = GCHandle.FromIntPtr((nint)userData);
        if (handle.Target is not VulkanValidationCollector collector)
        {
            return 0;
        }

        collector._messages.Enqueue(
            new(
                severity,
                type,
                callbackData->MessageIdNumber,
                Marshal.PtrToStringUTF8(
                    (nint)callbackData->PMessageIdName),
                Marshal.PtrToStringUTF8(
                    (nint)callbackData->PMessage)
                ?? string.Empty));
        return 0;
    }
}
