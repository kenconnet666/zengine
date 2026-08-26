namespace ZEngine.Plugins.Runtime;

public sealed class PluginGraphPlan
{
    private readonly Dictionary<string, PluginManifest> _manifests;
    private readonly Dictionary<string, string[]> _requiredConsumers;

    internal PluginGraphPlan(
        PluginManifest[] activationOrder,
        Dictionary<string, PluginManifest> manifests,
        Dictionary<string, string[]> requiredConsumers)
    {
        ActivationOrder = activationOrder;
        _manifests = manifests;
        _requiredConsumers = requiredConsumers;
    }

    public IReadOnlyList<PluginManifest> ActivationOrder { get; }

    public IReadOnlyList<string> ReverseRequiredClosure(string providerId)
    {
        if (!_manifests.ContainsKey(providerId))
        {
            throw new KeyNotFoundException($"Plugin '{providerId}' is not in the graph.");
        }

        HashSet<string> closure = new(StringComparer.Ordinal);
        Stack<string> pending = new();
        pending.Push(providerId);
        while (pending.TryPop(out string? current))
        {
            if (!closure.Add(current))
            {
                continue;
            }

            if (_requiredConsumers.TryGetValue(current, out string[]? consumers))
            {
                foreach (string consumer in consumers)
                {
                    pending.Push(consumer);
                }
            }
        }

        return ActivationOrder
            .Select(manifest => manifest.Id)
            .Where(closure.Contains)
            .Reverse()
            .ToArray();
    }
}

public static class PluginGraph
{
    public static PluginGraphPlan Resolve(
        IEnumerable<PluginManifest> manifests,
        bool includeDevelopmentDependencies = false)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        Dictionary<string, PluginManifest> byId = new(StringComparer.Ordinal);
        foreach (PluginManifest manifest in manifests)
        {
            ValidateManifest(manifest);
            if (!byId.TryAdd(manifest.Id, manifest))
            {
                throw new InvalidOperationException(
                    $"Plugin '{manifest.Id}' is declared more than once.");
            }
        }

        Dictionary<string, HashSet<string>> dependencies = new(StringComparer.Ordinal);
        Dictionary<string, List<string>> consumers = new(StringComparer.Ordinal);
        foreach (PluginManifest manifest in byId.Values)
        {
            HashSet<string> edges = new(StringComparer.Ordinal);
            dependencies.Add(manifest.Id, edges);
            foreach (PluginDependency dependency in manifest.Dependencies)
            {
                if (dependency.Kind == PluginDependencyKind.Development
                    && !includeDevelopmentDependencies)
                {
                    continue;
                }

                if (!byId.TryGetValue(dependency.Id, out PluginManifest? provider))
                {
                    if (dependency.Kind == PluginDependencyKind.Required)
                    {
                        throw new InvalidOperationException(
                            $"Plugin '{manifest.Id}' requires missing plugin '{dependency.Id}'.");
                    }

                    continue;
                }

                VersionRange range = VersionRange.Parse(dependency.VersionRange);
                if (!range.Contains(provider.SemanticVersion))
                {
                    if (dependency.Kind == PluginDependencyKind.Required)
                    {
                        throw new InvalidOperationException(
                            $"Plugin '{manifest.Id}' requires '{dependency.Id}' {dependency.VersionRange}, but resolved {provider.Version}.");
                    }

                    continue;
                }

                edges.Add(provider.Id);
                if (dependency.Kind == PluginDependencyKind.Required)
                {
                    if (!consumers.TryGetValue(provider.Id, out List<string>? list))
                    {
                        list = [];
                        consumers.Add(provider.Id, list);
                    }

                    list.Add(manifest.Id);
                }
            }
        }

        DetectCycles(dependencies);
        PluginManifest[] order = TopologicalOrder(byId, dependencies);
        return new(
            order,
            byId,
            consumers.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.Ordinal));
    }

    private static PluginManifest[] TopologicalOrder(
        IReadOnlyDictionary<string, PluginManifest> manifests,
        IReadOnlyDictionary<string, HashSet<string>> dependencies)
    {
        Dictionary<string, int> remaining = dependencies.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Count,
            StringComparer.Ordinal);
        Dictionary<string, List<string>> dependents = new(StringComparer.Ordinal);
        foreach ((string consumer, HashSet<string> providers) in dependencies)
        {
            foreach (string provider in providers)
            {
                if (!dependents.TryGetValue(provider, out List<string>? list))
                {
                    list = [];
                    dependents.Add(provider, list);
                }

                list.Add(consumer);
            }
        }

        PriorityQueue<string, string> ready = new(StringComparer.Ordinal);
        foreach ((string id, int count) in remaining)
        {
            if (count == 0)
            {
                ready.Enqueue(id, id);
            }
        }

        List<PluginManifest> order = [];
        while (ready.TryDequeue(out string? id, out _))
        {
            order.Add(manifests[id]);
            if (!dependents.TryGetValue(id, out List<string>? children))
            {
                continue;
            }

            foreach (string child in children)
            {
                int count = --remaining[child];
                if (count == 0)
                {
                    ready.Enqueue(child, child);
                }
            }
        }

        return order.ToArray();
    }

    private static void DetectCycles(
        IReadOnlyDictionary<string, HashSet<string>> dependencies)
    {
        Dictionary<string, int> state = new(StringComparer.Ordinal);
        List<string> path = [];
        foreach (string plugin in dependencies.Keys)
        {
            Visit(plugin);
        }

        void Visit(string plugin)
        {
            if (state.TryGetValue(plugin, out int existing))
            {
                if (existing == 1)
                {
                    int cycleStart = path.IndexOf(plugin);
                    string cycle = string.Join(
                        " -> ",
                        path.Skip(cycleStart).Append(plugin));
                    throw new InvalidOperationException(
                        $"Plugin dependency cycle detected: {cycle}.");
                }

                return;
            }

            state[plugin] = 1;
            path.Add(plugin);
            foreach (string dependency in dependencies[plugin])
            {
                Visit(dependency);
            }

            path.RemoveAt(path.Count - 1);
            state[plugin] = 2;
        }
    }

    private static void ValidateManifest(PluginManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Id);
        _ = manifest.SemanticVersion;
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.EntryAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.EntryType);
    }
}
