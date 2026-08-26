using System.Security.Cryptography;
using ZEngine.Core;

namespace ZEngine.Plugins.Runtime;

public sealed record PluginPackage(
    PluginManifest Manifest,
    string SourceDirectory);

public sealed record StagedPluginPackage(
    PluginManifest Manifest,
    string GenerationDirectory,
    string EntryAssemblyPath);

public sealed class ImmutableGenerationStore(string rootDirectory)
{
    private readonly string _rootDirectory = Path.GetFullPath(rootDirectory);

    public StagedPluginPackage Stage(
        PluginPackage package,
        GenerationId generation)
    {
        ArgumentNullException.ThrowIfNull(package);
        string sourceDirectory = Path.GetFullPath(package.SourceDirectory);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(sourceDirectory);
        }

        string destination = Path.Combine(
            _rootDirectory,
            generation.Value.ToString("D20"),
            "plugins",
            package.Manifest.Id,
            "runtime");
        if (Directory.Exists(destination))
        {
            throw new IOException(
                $"Immutable generation path already exists: {destination}");
        }

        Directory.CreateDirectory(destination);
        foreach (string sourceFile in Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, sourceFile);
            string destinationFile = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, overwrite: false);
        }

        string entryAssembly = Path.GetFullPath(
            Path.Combine(destination, package.Manifest.EntryAssembly));
        if (!entryAssembly.StartsWith(
                destination + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || !File.Exists(entryAssembly))
        {
            throw new FileNotFoundException(
                "Staged plugin entry assembly is missing or escaped its generation directory.",
                entryAssembly);
        }

        if (!string.IsNullOrWhiteSpace(package.Manifest.ContentSha256))
        {
            string actual = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(entryAssembly)));
            if (!string.Equals(
                    actual,
                    package.Manifest.ContentSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Plugin {package.Manifest.Id} content hash mismatch.");
            }
        }

        return new(
            package.Manifest,
            destination,
            entryAssembly);
    }
}
