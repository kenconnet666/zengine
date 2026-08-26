using System.IO.Compression;
using System.Text;
using ZEngine.Vulkan.Raw;
using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Graphics.Vulkan;

public sealed class VulkanFrameCaptureFactory : IVulkanFrameCaptureFactory
{
    public IVulkanFrameCapture Create(
        VulkanDevice device,
        VulkanGpuAllocator allocator,
        VkExtent2D extent,
        VkFormat format) =>
        new VulkanFrameCapture(device, allocator, extent, format);
}

internal sealed unsafe class VulkanFrameCapture : IVulkanFrameCapture
{
    private readonly VulkanBuffer _readback;
    private readonly VkExtent2D _extent;
    private readonly VkFormat _format;
    private readonly delegate* unmanaged<VkCommandBuffer, VkImage, VkImageLayout, VkBuffer, uint, VkBufferImageCopy*, void>
        _cmdCopyImageToBuffer;
    private ulong _lastSubmitted;

    public VulkanFrameCapture(
        VulkanDevice device,
        VulkanGpuAllocator allocator,
        VkExtent2D extent,
        VkFormat format)
    {
        _extent = extent;
        _format = format;
        if (format is not (VkFormat.B8G8R8A8Srgb or VkFormat.B8G8R8A8Unorm))
        {
            throw new NotSupportedException(
                $"P5 capture currently supports BGRA8 swapchains, not {format}.");
        }

        ulong size = checked((ulong)extent.Width * extent.Height * 4);
        _readback = VulkanBuffer.Create(
            device,
            allocator,
            size,
            VkBufferUsageFlags.TransferDestination,
            GpuMemoryClass.Readback);
        _cmdCopyImageToBuffer =
            (delegate* unmanaged<VkCommandBuffer, VkImage, VkImageLayout, VkBuffer, uint, VkBufferImageCopy*, void>)device.GetProcAddress(
                "vkCmdCopyImageToBuffer\0"u8);
    }

    public void Capture(
        VkCommandBuffer commandBuffer,
        VkImage image,
        VkExtent2D extent)
    {
        if (extent.Width != _extent.Width
            || extent.Height != _extent.Height)
        {
            throw new InvalidOperationException(
                "Capture extent differs from its swapchain generation.");
        }

        VkBufferImageCopy region = new()
        {
            ImageSubresource = new()
            {
                AspectMask = VkImageAspectFlags.Color,
                LayerCount = 1
            },
            ImageExtent = new()
            {
                Width = extent.Width,
                Height = extent.Height,
                Depth = 1
            }
        };
        _cmdCopyImageToBuffer(
            commandBuffer,
            image,
            VkImageLayout.TransferSourceOptimal,
            _readback.Handle,
            1,
            &region);
    }

    public void OnSubmitted(ulong timelineValue) =>
        Volatile.Write(ref _lastSubmitted, timelineValue);

    public byte[] ReadPng()
    {
        if (Volatile.Read(ref _lastSubmitted) == 0)
        {
            throw new InvalidOperationException(
                "No captured frame has completed submission.");
        }

        return PngEncoder.EncodeBgra(
            _readback.GetMappedBytes(),
            checked((int)_extent.Width),
            checked((int)_extent.Height));
    }

    public void Dispose() => _readback.Dispose();
}

public static class PngEncoder
{
    private static readonly byte[] Signature =
        [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] EncodeBgra(
        ReadOnlySpan<byte> bgra,
        int width,
        int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        int expected = checked(width * height * 4);
        if (bgra.Length < expected)
        {
            throw new ArgumentException(
                "BGRA source is smaller than width * height * 4.",
                nameof(bgra));
        }

        byte[] filtered = new byte[checked((width * 4 + 1) * height)];
        for (int y = 0; y < height; y++)
        {
            int sourceRow = y * width * 4;
            int destinationRow = y * (width * 4 + 1);
            filtered[destinationRow] = 0;
            for (int x = 0; x < width; x++)
            {
                int source = sourceRow + x * 4;
                int destination = destinationRow + 1 + x * 4;
                filtered[destination] = bgra[source + 2];
                filtered[destination + 1] = bgra[source + 1];
                filtered[destination + 2] = bgra[source];
                filtered[destination + 3] = bgra[source + 3];
            }
        }

        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(filtered);
        }

        using MemoryStream png = new();
        png.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IDAT"u8, compressed.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(
        Stream destination,
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        WriteBigEndian(length, 0, checked((uint)data.Length));
        destination.Write(length);
        destination.Write(type);
        destination.Write(data);
        uint crc = Crc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        destination.Write(crcBytes);
    }

    private static uint Crc32(
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        Update(type);
        Update(data);
        return ~crc;

        void Update(ReadOnlySpan<byte> values)
        {
            foreach (byte value in values)
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc >> 1) ^ (0xEDB88320u &
                        unchecked((uint)-(int)(crc & 1)));
                }
            }
        }
    }

    private static void WriteBigEndian(
        Span<byte> destination,
        int offset,
        uint value)
    {
        destination[offset] = (byte)(value >> 24);
        destination[offset + 1] = (byte)(value >> 16);
        destination[offset + 2] = (byte)(value >> 8);
        destination[offset + 3] = (byte)value;
    }
}
