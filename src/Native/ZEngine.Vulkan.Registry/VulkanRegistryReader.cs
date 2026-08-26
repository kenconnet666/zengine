using System.Xml.Linq;

namespace ZEngine.Vulkan.Registry;

public static class VulkanRegistryReader
{
    private static readonly HashSet<string> DeviceDispatchRoots =
    [
        "VkDevice",
        "VkQueue",
        "VkCommandBuffer"
    ];

    private static readonly HashSet<string> InstanceDispatchRoots =
    [
        "VkInstance",
        "VkPhysicalDevice"
    ];

    public static VulkanRegistrySnapshot Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XDocument document = XDocument.Load(stream, LoadOptions.SetLineInfo);
        XElement root = document.Root
            ?? throw new InvalidDataException("The Vulkan registry has no root element.");

        VulkanFeature[] features = root
            .Elements("feature")
            .Where(IsVulkanElement)
            .Select(ReadFeature)
            .OrderBy(feature => feature.Version)
            .ToArray();

        XElement[] handleElements = (root.Element("types")?.Elements("type")
                ?? [])
            .Where(element => (string?)element.Attribute("category") == "handle")
            .Where(IsVulkanElement)
            .ToArray();

        Dictionary<string, VulkanHandle> handlesByName =
            new(StringComparer.Ordinal);

        foreach (XElement element in handleElements.Where(
                     element => element.Attribute("alias") is null))
        {
            VulkanHandle handle = ReadHandle(element);
            handlesByName.Add(handle.Name, handle);
        }

        foreach (XElement element in handleElements.Where(
                     element => element.Attribute("alias") is not null))
        {
            string name = RequiredAttribute(element, "name");
            string alias = RequiredAttribute(element, "alias");
            if (!handlesByName.TryGetValue(alias, out VulkanHandle? target))
            {
                throw new InvalidDataException(
                    $"Handle alias '{name}' targets unknown handle '{alias}'.");
            }

            handlesByName.Add(
                name,
                new(name, target.IsDispatchable, target.Parent, alias));
        }

        VulkanHandle[] handles = handlesByName.Values
            .OrderBy(handle => handle.Name, StringComparer.Ordinal)
            .ToArray();

        XElement[] commandElements = (root.Element("commands")?.Elements("command")
                ?? [])
            .Where(IsVulkanElement)
            .ToArray();

        Dictionary<string, VulkanCommand> commandsByName =
            new(StringComparer.Ordinal);

        foreach (XElement element in commandElements.Where(
                     element => element.Attribute("alias") is null))
        {
            VulkanCommand command = ReadCommand(element);
            commandsByName.Add(command.Name, command);
        }

        foreach (XElement element in commandElements.Where(
                     element => element.Attribute("alias") is not null))
        {
            string name = RequiredAttribute(element, "name");
            string alias = RequiredAttribute(element, "alias");
            VulkanCommandScope scope = commandsByName.TryGetValue(alias, out VulkanCommand? target)
                ? target.Scope
                : VulkanCommandScope.Unknown;

            commandsByName.Add(name, new(name, scope, alias));
        }

        VulkanCommand[] commands = commandsByName.Values
            .OrderBy(command => command.Name, StringComparer.Ordinal)
            .ToArray();

        return new(features, handles, commands);
    }

    private static bool IsVulkanElement(XElement element)
    {
        string? api = (string?)element.Attribute("api");
        return api is null
            || api.Split(',', StringSplitOptions.TrimEntries).Contains(
                "vulkan",
                StringComparer.Ordinal);
    }

    private static VulkanFeature ReadFeature(XElement element)
    {
        string name = RequiredAttribute(element, "name");
        string number = RequiredAttribute(element, "number");

        return Version.TryParse(number, out Version? version)
            ? new(name, version)
            : throw new InvalidDataException(
                $"Feature '{name}' has invalid version '{number}'.");
    }

    private static VulkanHandle ReadHandle(XElement element)
    {
        string name = element.Element("name")?.Value
            ?? throw new InvalidDataException("A Vulkan handle type has no name.");
        string macro = element.Element("type")?.Value
            ?? throw new InvalidDataException($"Handle '{name}' has no declaration macro.");

        return new(
            name,
            string.Equals(macro, "VK_DEFINE_HANDLE", StringComparison.Ordinal),
            (string?)element.Attribute("parent"),
            null);
    }

    private static VulkanCommand ReadCommand(XElement element)
    {
        string name = element.Element("proto")?.Element("name")?.Value
            ?? throw new InvalidDataException("A Vulkan command has no prototype name.");
        string? firstParameterType = element.Elements("param")
            .FirstOrDefault()
            ?.Element("type")
            ?.Value;

        VulkanCommandScope scope = firstParameterType switch
        {
            null => VulkanCommandScope.Global,
            _ when DeviceDispatchRoots.Contains(firstParameterType) =>
                VulkanCommandScope.Device,
            _ when InstanceDispatchRoots.Contains(firstParameterType) =>
                VulkanCommandScope.Instance,
            _ => VulkanCommandScope.Global
        };

        return new(name, scope, null);
    }

    private static string RequiredAttribute(XElement element, string name) =>
        (string?)element.Attribute(name)
        ?? throw new InvalidDataException(
            $"Element '{element.Name}' has no required '{name}' attribute.");
}
