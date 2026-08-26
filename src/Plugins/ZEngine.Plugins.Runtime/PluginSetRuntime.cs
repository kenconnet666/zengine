using ZEngine.Jobs;

namespace ZEngine.Plugins.Runtime;

public sealed record PluginSetReloadResult(
    PluginReloadStatus Status,
    IReadOnlyList<string> ReloadedPlugins,
    string? Error);

public sealed class PluginSetRuntime : IAsyncDisposable
{
    private readonly string _generationRoot;
    private readonly JobScheduler _jobs;
    private PluginSetSnapshot? _active;
    private bool _disposed;

    public PluginSetRuntime(string generationRoot, int? workerCount = null)
    {
        _generationRoot = Path.GetFullPath(generationRoot);
        _jobs = new(workerCount);
    }

    public async Task LoadInitialAsync(
        IEnumerable<PluginPackage> packages,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_active is not null)
        {
            throw new InvalidOperationException("A plugin set is already active.");
        }

        Dictionary<string, PluginPackage> packageMap = packages.ToDictionary(
            package => package.Manifest.Id,
            StringComparer.Ordinal);
        PluginGraphPlan graph = PluginGraph.Resolve(
            packageMap.Values.Select(package => package.Manifest));
        Dictionary<string, PluginSetEntry> entries = await StageEntriesAsync(
            graph.ActivationOrder.Select(manifest => manifest.Id),
            packageMap,
            baseEntries: null,
            cancellationToken);
        Volatile.Write(
            ref _active,
            CreateSnapshot(packageMap, graph, entries));
    }

    public async Task<PluginSetReloadResult> ReloadClosureAsync(
        string pluginId,
        PluginPackage replacement,
        Func<IReadOnlyDictionary<Type, object>, bool>? probation = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PluginSetSnapshot old = Volatile.Read(ref _active)
            ?? throw new InvalidOperationException("No plugin set is active.");
        if (!string.Equals(
                pluginId,
                replacement.Manifest.Id,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Replacement package id does not match the requested plugin.",
                nameof(replacement));
        }

        Dictionary<string, PluginPackage> packages = new(
            old.Packages,
            StringComparer.Ordinal)
        {
            [pluginId] = replacement
        };
        PluginGraphPlan graph;
        IReadOnlyList<string> unloadClosure;
        try
        {
            graph = PluginGraph.Resolve(
                packages.Values.Select(package => package.Manifest));
            unloadClosure = graph.ReverseRequiredClosure(pluginId);
        }
        catch (Exception exception)
        {
            return new(PluginReloadStatus.Rejected, [], exception.Message);
        }

        HashSet<string> reloadSet = unloadClosure.ToHashSet(
            StringComparer.Ordinal);
        string[] activationClosure = graph.ActivationOrder
            .Select(manifest => manifest.Id)
            .Where(reloadSet.Contains)
            .ToArray();
        Dictionary<string, PluginSetEntry>? staged = null;
        try
        {
            staged = await StageEntriesAsync(
                activationClosure,
                packages,
                old.Entries
                    .Where(pair => !reloadSet.Contains(pair.Key))
                    .ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.Ordinal),
                cancellationToken);
            Dictionary<string, PluginSetEntry> merged = new(
                old.Entries,
                StringComparer.Ordinal);
            foreach ((string id, PluginSetEntry entry) in staged)
            {
                merged[id] = entry;
            }

            PluginSetSnapshot next = CreateSnapshot(packages, graph, merged);
            Volatile.Write(ref _active, next);
            if (probation is not null && !probation(next.Services))
            {
                Volatile.Write(ref _active, old);
                await DisposeEntriesAsync(
                    activationClosure.Reverse(),
                    staged);
                return new(
                    PluginReloadStatus.RolledBack,
                    activationClosure,
                    "Plugin-set probation rejected the staged closure.");
            }

            await DisposeEntriesAsync(unloadClosure, old.Entries);
            return new(
                PluginReloadStatus.Applied,
                activationClosure,
                null);
        }
        catch (Exception exception)
        {
            if (staged is not null)
            {
                await DisposeEntriesAsync(
                    activationClosure.Reverse(),
                    staged);
            }

            return new(
                PluginReloadStatus.Rejected,
                activationClosure,
                exception.Message);
        }
    }

    public TService GetService<TService>()
        where TService : class
    {
        PluginSetSnapshot active = Volatile.Read(ref _active)
            ?? throw new InvalidOperationException("No plugin set is active.");
        return active.Services.TryGetValue(typeof(TService), out object? service)
            ? (TService)service
            : throw new KeyNotFoundException(
                $"Plugin set does not export {typeof(TService).FullName}.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PluginSetSnapshot? active = Interlocked.Exchange(ref _active, null);
        if (active is not null)
        {
            IEnumerable<string> unloadOrder = active.Graph.ActivationOrder
                .Select(manifest => manifest.Id)
                .Reverse();
            await DisposeEntriesAsync(unloadOrder, active.Entries);
        }

        _jobs.Dispose();
    }

    private async Task<Dictionary<string, PluginSetEntry>> StageEntriesAsync(
        IEnumerable<string> activationOrder,
        IReadOnlyDictionary<string, PluginPackage> packages,
        IReadOnlyDictionary<string, PluginSetEntry>? baseEntries,
        CancellationToken cancellationToken)
    {
        Dictionary<string, PluginSetEntry> staged = baseEntries is null
            ? new(StringComparer.Ordinal)
            : new(baseEntries, StringComparer.Ordinal);
        List<string> created = [];
        string transactionRoot = Path.Combine(
            _generationRoot,
            "transactions",
            Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string pluginId in activationOrder)
            {
                IReadOnlyDictionary<Type, object> imports = BuildServiceRegistry(
                    staged.Values);
                PluginRuntime runtime = new(
                    Path.Combine(transactionRoot, pluginId),
                    _jobs);
                await runtime.LoadInitialAsync(
                    packages[pluginId],
                    cancellationToken,
                    imports);
                staged[pluginId] = new(runtime, packages[pluginId]);
                created.Add(pluginId);
            }

            return created.ToDictionary(
                id => id,
                id => staged[id],
                StringComparer.Ordinal);
        }
        catch
        {
            await DisposeEntriesAsync(created.AsEnumerable().Reverse(), staged);
            throw;
        }
    }

    private static PluginSetSnapshot CreateSnapshot(
        Dictionary<string, PluginPackage> packages,
        PluginGraphPlan graph,
        Dictionary<string, PluginSetEntry> entries) =>
        new(
            packages,
            graph,
            entries,
            BuildServiceRegistry(entries.Values));

    private static IReadOnlyDictionary<Type, object> BuildServiceRegistry(
        IEnumerable<PluginSetEntry> entries)
    {
        Dictionary<Type, object> services = [];
        foreach (PluginSetEntry entry in entries)
        {
            foreach ((Type contract, object service) in entry.Runtime.GetExportSnapshot())
            {
                if (!services.TryAdd(contract, service))
                {
                    throw new InvalidOperationException(
                        $"Multiple plugins export service contract {contract.FullName}.");
                }
            }
        }

        return services;
    }

    private static async Task DisposeEntriesAsync(
        IEnumerable<string> order,
        IReadOnlyDictionary<string, PluginSetEntry> entries)
    {
        foreach (string pluginId in order)
        {
            if (entries.TryGetValue(pluginId, out PluginSetEntry? entry))
            {
                await entry.Runtime.DisposeAsync();
            }
        }
    }

    private sealed record PluginSetSnapshot(
        Dictionary<string, PluginPackage> Packages,
        PluginGraphPlan Graph,
        Dictionary<string, PluginSetEntry> Entries,
        IReadOnlyDictionary<Type, object> Services);

    private sealed record PluginSetEntry(
        PluginRuntime Runtime,
        PluginPackage Package);
}
