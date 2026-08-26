namespace ZEngine.Vulkan.Raw;

public readonly record struct VulkanApiVersion(uint Value)
{
    private const int VariantShift = 29;
    private const int MajorShift = 22;
    private const int MinorShift = 12;

    public uint Variant => Value >> VariantShift;

    public uint Major => (Value >> MajorShift) & 0x7f;

    public uint Minor => (Value >> MinorShift) & 0x3ff;

    public uint Patch => Value & 0xfff;

    public static VulkanApiVersion Create(
        uint major,
        uint minor,
        uint patch,
        uint variant = 0) =>
        new(
            (variant << VariantShift)
            | (major << MajorShift)
            | (minor << MinorShift)
            | patch);

    public override string ToString() =>
        Variant == 0
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Variant}:{Major}.{Minor}.{Patch}";
}
