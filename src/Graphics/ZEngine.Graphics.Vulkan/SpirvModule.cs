using System.Buffers.Binary;

namespace ZEngine.Graphics.Vulkan;

public static class SpirvModule
{
    public const uint Magic = 0x07230203;

    public static void Validate(ReadOnlySpan<byte> module, string parameterName)
    {
        if (module.Length < 20 || module.Length % sizeof(uint) != 0)
        {
            throw new ArgumentException(
                "A SPIR-V module must contain a complete five-word header and whole 32-bit words.",
                parameterName);
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(module) != Magic)
        {
            throw new ArgumentException(
                "The shader module does not start with the SPIR-V magic number.",
                parameterName);
        }
    }
}

public enum ShaderReloadStatus
{
    Unchanged,
    Applied,
    Rejected
}

public sealed record ShaderReloadResult(
    ShaderReloadStatus Status,
    uint Generation,
    string? Error = null);
