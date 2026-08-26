using System.Security.Cryptography;
using System.Text.Json;

namespace ZEngine.Plugins.Runtime;

public sealed record PluginLockDependency(
    string Id,
    string Version);

public sealed record PluginLockEntry(
    string Id,
    string Version,
    string ContentSha256,
    IReadOnlyList<PluginLockDependency> Dependencies);

public sealed record PluginLockFile(
    int FormatVersion,
    IReadOnlyList<PluginLockEntry> Plugins)
{
    public const int CurrentFormatVersion = 1;

    public static PluginLockFile Create(IEnumerable<PluginPackage> packages)
    {
        Dictionary<string, PluginPackage> byId = packages.ToDictionary(
            package => package.Manifest.Id,
            StringComparer.Ordinal);
        _ = PluginGraph.Resolve(byId.Values.Select(package => package.Manifest));
        PluginLockEntry[] entries = byId.Values
            .OrderBy(package => package.Manifest.Id, StringComparer.Ordinal)
            .Select(package =>
            {
                string entryPath = Path.Combine(
                    Path.GetFullPath(package.SourceDirectory),
                    package.Manifest.EntryAssembly);
                string hash = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(entryPath)));
                PluginLockDependency[] dependencies = package.Manifest.Dependencies
                    .Where(dependency =>
                        dependency.Kind == PluginDependencyKind.Required)
                    .OrderBy(dependency => dependency.Id, StringComparer.Ordinal)
                    .Select(dependency => new PluginLockDependency(
                        dependency.Id,
                        byId[dependency.Id].Manifest.Version))
                    .ToArray();
                return new PluginLockEntry(
                    package.Manifest.Id,
                    package.Manifest.Version,
                    hash,
                    dependencies);
            })
            .ToArray();
        return new(CurrentFormatVersion, entries);
    }

    public void Validate(IEnumerable<PluginPackage> packages)
    {
        if (FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Plugin lock format {FormatVersion} is not supported.");
        }

        Dictionary<string, PluginPackage> packageMap = packages.ToDictionary(
            package => package.Manifest.Id,
            StringComparer.Ordinal);
        Dictionary<string, PluginLockEntry> lockMap = Plugins.ToDictionary(
            entry => entry.Id,
            StringComparer.Ordinal);
        if (!packageMap.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(lockMap.Keys))
        {
            throw new InvalidDataException(
                "Plugin lock ids do not exactly match the package set.");
        }

        foreach ((string id, PluginPackage package) in packageMap)
        {
            PluginLockEntry entry = lockMap[id];
            if (!string.Equals(
                    entry.Version,
                    package.Manifest.Version,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Plugin {id} version differs from plugin.lock.json.");
            }

            string entryPath = Path.Combine(
                Path.GetFullPath(package.SourceDirectory),
                package.Manifest.EntryAssembly);
            string actualHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(entryPath)));
            if (!string.Equals(
                    actualHash,
                    entry.ContentSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Plugin {id} content differs from plugin.lock.json.");
            }

            PluginLockDependency[] expectedDependencies = package.Manifest.Dependencies
                .Where(dependency =>
                    dependency.Kind == PluginDependencyKind.Required)
                .OrderBy(dependency => dependency.Id, StringComparer.Ordinal)
                .Select(dependency => new PluginLockDependency(
                    dependency.Id,
                    packageMap[dependency.Id].Manifest.Version))
                .ToArray();
            if (!entry.Dependencies.SequenceEqual(expectedDependencies))
            {
                throw new InvalidDataException(
                    $"Plugin {id} exact dependencies differ from plugin.lock.json.");
            }
        }
    }

    public void Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        JsonSerializer.Serialize(
            destination,
            this,
            PluginJsonContext.Default.PluginLockFile);
    }

    public static PluginLockFile Load(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return JsonSerializer.Deserialize(
                   source,
                   PluginJsonContext.Default.PluginLockFile)
               ?? throw new InvalidDataException("plugin.lock.json is empty.");
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(PluginLockFile))]
internal partial class PluginJsonContext
    : System.Text.Json.Serialization.JsonSerializerContext;
