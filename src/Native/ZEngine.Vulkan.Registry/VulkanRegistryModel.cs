namespace ZEngine.Vulkan.Registry;

public enum VulkanCommandScope
{
    Unknown,
    Global,
    Instance,
    Device
}

public sealed record VulkanFeature(string Name, Version Version);

public sealed record VulkanHandle(
    string Name,
    bool IsDispatchable,
    string? Parent,
    string? Alias);

public sealed record VulkanCommand(
    string Name,
    VulkanCommandScope Scope,
    string? Alias);

public sealed class VulkanRegistrySnapshot(
    IReadOnlyList<VulkanFeature> coreFeatures,
    IReadOnlyList<VulkanHandle> handles,
    IReadOnlyList<VulkanCommand> commands)
{
    public IReadOnlyList<VulkanFeature> CoreFeatures { get; } = coreFeatures;

    public IReadOnlyList<VulkanHandle> Handles { get; } = handles;

    public IReadOnlyList<VulkanCommand> Commands { get; } = commands;

    public VulkanFeature HighestCoreFeature =>
        CoreFeatures.Count == 0
            ? throw new InvalidOperationException("The registry contains no Vulkan core features.")
            : CoreFeatures[^1];
}
