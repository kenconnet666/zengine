using ZEngine.Graphics;

namespace ZEngine.RenderGraph;

internal static class RenderGraphCompiler
{
    public static CompiledRenderGraph Compile(
        IReadOnlyList<RenderResourceDefinition> resources,
        IReadOnlyList<RenderPassDefinition> passes,
        IReadOnlySet<int> outputs)
    {
        Dictionary<string, int> passIndices = BuildPassIndices(passes);
        HashSet<int>[] dependencies = CreateSets(passes.Count);
        int[] lastWriters = new int[resources.Count];
        Array.Fill(lastWriters, -1);
        HashSet<int>[] readers = CreateSets(resources.Count);

        BuildResourceDependencies(
            passes,
            dependencies,
            lastWriters,
            readers);
        BuildExplicitDependencies(
            passes,
            passIndices,
            dependencies);

        bool[] live = FindLivePasses(
            passes,
            dependencies,
            lastWriters,
            outputs);
        int[] schedule = TopologicalSchedule(
            passes,
            dependencies,
            live);
        CompiledRenderPass[] compiledPasses = BuildCompiledPasses(
            resources,
            passes,
            dependencies,
            live,
            schedule);

        BuildBarriersAndLifetimes(
            resources,
            passes,
            schedule,
            out RenderBarrier[] barriers,
            out RenderBarrier[][] barriersByPass,
            out ResourceLifetime[] lifetimes);

        RenderRecordingBatch[] batches = BuildRecordingBatches(
            compiledPasses,
            dependencies,
            live);
        string[] culled = passes
            .Where(pass => !live[pass.Index])
            .Select(pass => pass.Name)
            .ToArray();

        return new(
            compiledPasses,
            barriers,
            barriersByPass,
            lifetimes,
            batches,
            culled);
    }

    private static Dictionary<string, int> BuildPassIndices(
        IReadOnlyList<RenderPassDefinition> passes)
    {
        Dictionary<string, int> result = new(
            passes.Count,
            StringComparer.Ordinal);
        foreach (RenderPassDefinition pass in passes)
        {
            result.Add(pass.Name, pass.Index);
        }

        return result;
    }

    private static void BuildResourceDependencies(
        IReadOnlyList<RenderPassDefinition> passes,
        HashSet<int>[] dependencies,
        int[] lastWriters,
        HashSet<int>[] readers)
    {
        foreach (RenderPassDefinition pass in passes)
        {
            foreach (RenderResourceAccess access in pass.Accesses)
            {
                int writer = lastWriters[access.ResourceIndex];
                if (writer >= 0)
                {
                    dependencies[pass.Index].Add(writer);
                }

                if (!access.Writes)
                {
                    readers[access.ResourceIndex].Add(pass.Index);
                    continue;
                }

                foreach (int reader in readers[access.ResourceIndex])
                {
                    if (reader != pass.Index)
                    {
                        dependencies[pass.Index].Add(reader);
                    }
                }

                readers[access.ResourceIndex].Clear();
                lastWriters[access.ResourceIndex] = pass.Index;
            }
        }
    }

    private static void BuildExplicitDependencies(
        IReadOnlyList<RenderPassDefinition> passes,
        IReadOnlyDictionary<string, int> passIndices,
        HashSet<int>[] dependencies)
    {
        foreach (RenderPassDefinition pass in passes)
        {
            foreach (string after in pass.After)
            {
                if (!passIndices.TryGetValue(after, out int target))
                {
                    throw new RenderGraphCompileException(
                        $"Render pass '{pass.Name}' requires missing pass '{after}'.");
                }

                dependencies[pass.Index].Add(target);
            }

            foreach (string before in pass.Before)
            {
                if (!passIndices.TryGetValue(before, out int target))
                {
                    throw new RenderGraphCompileException(
                        $"Render pass '{pass.Name}' requires missing pass '{before}'.");
                }

                dependencies[target].Add(pass.Index);
            }
        }
    }

    private static bool[] FindLivePasses(
        IReadOnlyList<RenderPassDefinition> passes,
        HashSet<int>[] dependencies,
        int[] lastWriters,
        IReadOnlySet<int> outputs)
    {
        bool[] live = new bool[passes.Count];
        Stack<int> pending = new();

        foreach (RenderPassDefinition pass in passes)
        {
            if (pass.HasSideEffects)
            {
                pending.Push(pass.Index);
            }
        }

        foreach (int output in outputs)
        {
            int writer = lastWriters[output];
            if (writer >= 0)
            {
                pending.Push(writer);
            }
        }

        while (pending.TryPop(out int passIndex))
        {
            if (live[passIndex])
            {
                continue;
            }

            live[passIndex] = true;
            foreach (int dependency in dependencies[passIndex])
            {
                pending.Push(dependency);
            }
        }

        return live;
    }

    private static int[] TopologicalSchedule(
        IReadOnlyList<RenderPassDefinition> passes,
        HashSet<int>[] dependencies,
        bool[] live)
    {
        int liveCount = live.Count(value => value);
        int[] inDegree = new int[passes.Count];
        List<int>[] dependents = CreateLists(passes.Count);

        for (int passIndex = 0; passIndex < passes.Count; passIndex++)
        {
            if (!live[passIndex])
            {
                continue;
            }

            foreach (int dependency in dependencies[passIndex])
            {
                if (!live[dependency])
                {
                    continue;
                }

                inDegree[passIndex]++;
                dependents[dependency].Add(passIndex);
            }
        }

        PriorityQueue<int, int> ready = new();
        for (int passIndex = 0; passIndex < passes.Count; passIndex++)
        {
            if (live[passIndex] && inDegree[passIndex] == 0)
            {
                ready.Enqueue(passIndex, passIndex);
            }
        }

        int[] schedule = new int[liveCount];
        int scheduled = 0;
        while (ready.TryDequeue(out int passIndex, out _))
        {
            schedule[scheduled++] = passIndex;
            foreach (int dependent in dependents[passIndex])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    ready.Enqueue(dependent, dependent);
                }
            }
        }

        if (scheduled != liveCount)
        {
            string cycle = string.Join(
                ", ",
                passes
                    .Where(pass => live[pass.Index] && inDegree[pass.Index] > 0)
                    .Select(pass => pass.Name));
            throw new RenderGraphCompileException(
                $"Render graph contains a dependency cycle involving: {cycle}.");
        }

        return schedule;
    }

    private static CompiledRenderPass[] BuildCompiledPasses(
        IReadOnlyList<RenderResourceDefinition> resources,
        IReadOnlyList<RenderPassDefinition> passes,
        HashSet<int>[] dependencies,
        bool[] live,
        int[] schedule)
    {
        CompiledRenderPass[] result = new CompiledRenderPass[schedule.Length];
        for (int index = 0; index < schedule.Length; index++)
        {
            RenderPassDefinition pass = passes[schedule[index]];
            string[] names = dependencies[pass.Index]
                .Where(dependency => live[dependency])
                .Order()
                .Select(dependency => passes[dependency].Name)
                .ToArray();
            CompiledResourceAccess[] accesses = pass.Accesses
                .Select(access => ToCompiledAccess(
                    resources[access.ResourceIndex],
                    access))
                .ToArray();
            result[index] = new(
                pass.Name,
                pass.Index,
                names,
                accesses,
                pass.Executor);
        }

        return result;
    }

    private static void BuildBarriersAndLifetimes(
        IReadOnlyList<RenderResourceDefinition> resources,
        IReadOnlyList<RenderPassDefinition> passes,
        int[] schedule,
        out RenderBarrier[] barriers,
        out RenderBarrier[][] barriersByPass,
        out ResourceLifetime[] lifetimes)
    {
        PreviousAccess?[] previous = new PreviousAccess?[resources.Count];
        for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
        {
            if (resources[resourceIndex].InitialAccess is { } initialAccess)
            {
                previous[resourceIndex] = new(-1, initialAccess);
            }
        }
        int[] firstUse = new int[resources.Count];
        int[] lastUse = new int[resources.Count];
        Array.Fill(firstUse, -1);
        Array.Fill(lastUse, -1);
        List<RenderBarrier> allBarriers = [];
        barriersByPass = new RenderBarrier[schedule.Length][];

        for (int scheduleIndex = 0; scheduleIndex < schedule.Length; scheduleIndex++)
        {
            RenderPassDefinition pass = passes[schedule[scheduleIndex]];
            List<RenderBarrier> passBarriers = [];
            foreach (RenderResourceAccess access in pass.Accesses)
            {
                firstUse[access.ResourceIndex] = firstUse[access.ResourceIndex] < 0
                    ? scheduleIndex
                    : firstUse[access.ResourceIndex];
                lastUse[access.ResourceIndex] = scheduleIndex;

                PreviousAccess? prior = previous[access.ResourceIndex];
                if (prior is { } source && NeedsBarrier(source.Access, access))
                {
                    RenderResourceDefinition resource = resources[access.ResourceIndex];
                    RenderBarrier barrier = new(
                        resource.Name,
                        ToPublicKind(resource.Kind),
                        source.PassIndex < 0
                            ? "<external>"
                            : passes[source.PassIndex].Name,
                        pass.Name,
                        source.Access.Stage,
                        source.Access.Access,
                        source.Access.Layout,
                        access.Stage,
                        access.Access,
                        access.Layout);
                    passBarriers.Add(barrier);
                    allBarriers.Add(barrier);
                }

                previous[access.ResourceIndex] = new(pass.Index, access);
            }

            barriersByPass[scheduleIndex] = passBarriers.ToArray();
        }

        barriers = allBarriers.ToArray();
        lifetimes = BuildLifetimes(
            resources,
            firstUse,
            lastUse);
    }

    private static ResourceLifetime[] BuildLifetimes(
        IReadOnlyList<RenderResourceDefinition> resources,
        int[] firstUse,
        int[] lastUse)
    {
        List<LifetimeBuilder> builders = [];
        for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
        {
            if (firstUse[resourceIndex] < 0)
            {
                continue;
            }

            RenderResourceDefinition resource = resources[resourceIndex];
            builders.Add(
                new(
                    resource,
                    firstUse[resourceIndex],
                    lastUse[resourceIndex]));
        }

        List<AliasSlot> slots = [];
        foreach (LifetimeBuilder lifetime in builders
                     .Where(candidate => candidate.Transient)
                     .OrderBy(candidate => candidate.FirstUse))
        {
            AliasSlot? available = slots.FirstOrDefault(slot =>
                slot.LastUse < lifetime.FirstUse
                && Equals(slot.CompatibilityKey, lifetime.CompatibilityKey));
            if (available is null)
            {
                available = new(
                    slots.Count,
                    lifetime.LastUse,
                    lifetime.CompatibilityKey);
                slots.Add(available);
            }
            else
            {
                available.LastUse = lifetime.LastUse;
            }

            lifetime.AliasSlot = available.Index;
        }

        return builders
            .Select(lifetime => new ResourceLifetime(
                lifetime.Resource.Name,
                ToPublicKind(lifetime.Resource.Kind),
                lifetime.FirstUse,
                lifetime.LastUse,
                lifetime.Transient,
                lifetime.AliasSlot))
            .ToArray();
    }

    private static RenderRecordingBatch[] BuildRecordingBatches(
        IReadOnlyList<CompiledRenderPass> passes,
        HashSet<int>[] dependencies,
        bool[] live)
    {
        int[] levels = new int[live.Length];
        Dictionary<int, List<CompiledRenderPass>> grouped = [];
        foreach (CompiledRenderPass pass in passes)
        {
            int level = 0;
            foreach (int dependency in dependencies[pass.OriginalIndex])
            {
                if (live[dependency])
                {
                    level = Math.Max(level, levels[dependency] + 1);
                }
            }

            levels[pass.OriginalIndex] = level;
            if (!grouped.TryGetValue(level, out List<CompiledRenderPass>? batch))
            {
                batch = [];
                grouped.Add(level, batch);
            }

            batch.Add(pass);
        }

        return grouped
            .OrderBy(pair => pair.Key)
            .Select(pair => new RenderRecordingBatch(pair.Key, pair.Value.ToArray()))
            .ToArray();
    }

    private static bool NeedsBarrier(
        RenderResourceAccess source,
        RenderResourceAccess destination) =>
        source.Writes
        || destination.Writes
        || source.Layout != destination.Layout;

    private static CompiledResourceKind ToPublicKind(RenderResourceKind kind) =>
        kind == RenderResourceKind.Image
            ? CompiledResourceKind.Image
            : CompiledResourceKind.Buffer;

    private static CompiledResourceAccess ToCompiledAccess(
        RenderResourceDefinition resource,
        RenderResourceAccess access) =>
        new(
            access.ResourceIndex,
            resource.Name,
            ToPublicKind(resource.Kind),
            access.Mode switch
            {
                RenderAccessMode.Read => CompiledAccessMode.Read,
                RenderAccessMode.Write => CompiledAccessMode.Write,
                RenderAccessMode.ReadWrite => CompiledAccessMode.ReadWrite,
                _ => throw new ArgumentOutOfRangeException(nameof(access))
            },
            access.Stage,
            access.Access,
            access.Layout,
            access.Load,
            access.Store);

    private static bool IsTransient(RenderResourceDefinition resource) =>
        !resource.Imported
        && resource.Descriptor switch
        {
            ImageDescriptor image => image.Transient,
            BufferDescriptor buffer => buffer.Transient,
            _ => false
        };

    private static HashSet<int>[] CreateSets(int count)
    {
        HashSet<int>[] result = new HashSet<int>[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = [];
        }

        return result;
    }

    private static List<int>[] CreateLists(int count)
    {
        List<int>[] result = new List<int>[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = [];
        }

        return result;
    }

    private readonly record struct PreviousAccess(
        int PassIndex,
        RenderResourceAccess Access);

    private sealed class LifetimeBuilder
    {
        public LifetimeBuilder(
            RenderResourceDefinition resource,
            int firstUse,
            int lastUse)
        {
            Resource = resource;
            FirstUse = firstUse;
            LastUse = lastUse;
            Transient = IsTransient(resource);
            CompatibilityKey = (resource.Kind, resource.Descriptor);
        }

        public RenderResourceDefinition Resource { get; }

        public int FirstUse { get; }

        public int LastUse { get; }

        public bool Transient { get; }

        public object CompatibilityKey { get; }

        public int AliasSlot { get; set; } = -1;
    }

    private sealed class AliasSlot(
        int index,
        int lastUse,
        object compatibilityKey)
    {
        public int Index { get; } = index;

        public int LastUse { get; set; } = lastUse;

        public object CompatibilityKey { get; } = compatibilityKey;
    }
}
