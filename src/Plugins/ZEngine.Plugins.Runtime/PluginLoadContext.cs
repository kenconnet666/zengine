using System.Reflection;
using System.Runtime.Loader;

namespace ZEngine.Plugins.Runtime;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string[] EngineSharedAssemblies =
    [
        "ZEngine.Plugins.Abstractions",
        "ZEngine.Core",
        "ZEngine.Jobs"
    ];

    private readonly AssemblyDependencyResolver _resolver;
    private readonly NativeReloadPolicy _nativeReloadPolicy;
    private readonly HashSet<string> _sharedAssemblies;

    public PluginLoadContext(
        string mainAssemblyPath,
        string pluginId,
        NativeReloadPolicy nativeReloadPolicy,
        IEnumerable<string> contractAssemblies)
        : base($"ZEngine.Plugin.{pluginId}.{Guid.NewGuid():N}", isCollectible: true)
    {
        _resolver = new(mainAssemblyPath);
        _nativeReloadPolicy = nativeReloadPolicy;
        _sharedAssemblies = new(
            EngineSharedAssemblies.Concat(contractAssemblies),
            StringComparer.Ordinal);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is { } name && _sharedAssemblies.Contains(name))
        {
            return Default.Assemblies.FirstOrDefault(assembly =>
                AssemblyName.ReferenceMatchesDefinition(
                    assembly.GetName(),
                    assemblyName));
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        if (_nativeReloadPolicy != NativeReloadPolicy.Reloadable)
        {
            throw new NotSupportedException(
                $"Plugin native library '{unmanagedDllName}' requires runtime restart under {_nativeReloadPolicy} policy.");
        }

        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? 0 : LoadUnmanagedDllFromPath(path);
    }
}
