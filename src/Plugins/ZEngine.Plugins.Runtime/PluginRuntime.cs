using System.Reflection;
using System.Runtime.CompilerServices;
using ZEngine.Core;
using ZEngine.Jobs;

namespace ZEngine.Plugins.Runtime;

public enum PluginReloadStatus
{
    Applied,
    Rejected,
    RolledBack
}

public sealed record PluginReloadResult(
    PluginReloadStatus Status,
    GenerationId ActiveGeneration,
    string? Error,
    WeakReference? UnloadedContext);

public sealed class PluginRuntime : IAsyncDisposable
{
    private readonly ImmutableGenerationStore _generationStore;
    private readonly JobScheduler _jobs;
    private readonly bool _ownsScheduler;
    private readonly List<WeakReference> _unloadedContexts = [];
    private PluginActivation? _current;
    private GenerationId _nextGeneration = GenerationId.Initial;
    private bool _disposed;

    public PluginRuntime(
        string generationRoot,
        JobScheduler? jobs = null)
    {
        _generationStore = new(generationRoot);
        _jobs = jobs ?? new();
        _ownsScheduler = jobs is null;
    }

    public GenerationId CurrentGeneration =>
        Volatile.Read(ref _current)?.Generation ?? default;

    public IReadOnlyList<WeakReference> UnloadedContexts => _unloadedContexts;

    public async Task LoadInitialAsync(
        PluginPackage package,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_current is not null)
        {
            throw new InvalidOperationException("A plugin generation is already active.");
        }

        PluginActivation activation = await StageAsync(
            package,
            NextGeneration(),
            cancellationToken);
        activation.Plugin!.OnActivated();
        Volatile.Write(ref _current, activation);
    }

    public async Task<PluginReloadResult> ReloadAsync(
        PluginPackage package,
        Func<EnginePlugin, bool>? probation = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PluginActivation old = Volatile.Read(ref _current)
            ?? throw new InvalidOperationException("No plugin generation is active.");
        PluginActivation? staged = null;
        try
        {
            staged = await StageAsync(
                package,
                NextGeneration(),
                cancellationToken);
            HotState state = new();
            old.Plugin!.SaveHotState(state.Writer());
            staged.Plugin!.RestoreHotState(state.Reader());
        }
        catch (Exception exception)
        {
            if (staged is not null)
            {
                _ = await ReleaseAsync(staged);
                staged = null;
            }

            return new(
                PluginReloadStatus.Rejected,
                old.Generation,
                exception.Message,
                null);
        }

        GenerationId oldGeneration = old.Generation;
        GenerationId stagedGeneration = staged.Generation;
        old.Scope!.Suspend();
        await old.Scope.WaitForIdleAsync();
        Volatile.Write(ref _current, staged);
        try
        {
            staged.Plugin!.OnActivated();
            if (probation is not null && !probation(staged.Plugin))
            {
                throw new InvalidOperationException(
                    "Plugin probation callback rejected the staged generation.");
            }
        }
        catch (Exception exception)
        {
            staged.Plugin!.OnDeactivated();
            Volatile.Write(ref _current, old);
            old.Scope.Resume();
            WeakReference rejectedContext = await ReleaseAsync(staged);
            _unloadedContexts.Add(rejectedContext);
            staged = null;
            old = null!;
            return new(
                PluginReloadStatus.RolledBack,
                oldGeneration,
                exception.Message,
                rejectedContext);
        }

        old.Plugin!.OnDeactivated();
        WeakReference unloaded = await ReleaseAsync(old);
        _unloadedContexts.Add(unloaded);
        old = null!;
        staged = null;
        return new(
            PluginReloadStatus.Applied,
            stagedGeneration,
            null,
            unloaded);
    }

    public TService GetService<TService>()
        where TService : class
    {
        PluginActivation activation = Volatile.Read(ref _current)
            ?? throw new InvalidOperationException("No plugin generation is active.");
        if (!activation.Scope!.Services.TryGetValue(
                typeof(TService),
                out object? service))
        {
            throw new KeyNotFoundException(
                $"Active plugin does not export {typeof(TService).FullName}.");
        }

        return (TService)service;
    }

    public static bool CollectAndCheck(WeakReference context)
    {
        ArgumentNullException.ThrowIfNull(context);
        for (int attempt = 0; attempt < 3 && context.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        return !context.IsAlive;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PluginActivation? current = Interlocked.Exchange(ref _current, null);
        if (current is not null)
        {
            current.Plugin!.OnDeactivated();
            WeakReference unloaded = await ReleaseAsync(current);
            _unloadedContexts.Add(unloaded);
            current = null;
        }

        if (_ownsScheduler)
        {
            _jobs.Dispose();
        }
    }

    private async Task<PluginActivation> StageAsync(
        PluginPackage package,
        GenerationId generation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StagedPluginPackage staged = _generationStore.Stage(package, generation);
        PluginLoadContext context = new(
            staged.EntryAssemblyPath,
            staged.Manifest.Id,
            staged.Manifest.NativeReloadPolicy,
            staged.Manifest.ContractAssemblies);
        PluginScope? scope = null;
        try
        {
            using FileStream assemblyStream = File.OpenRead(
                staged.EntryAssemblyPath);
            string symbolsPath = Path.ChangeExtension(
                staged.EntryAssemblyPath,
                ".pdb");
            Assembly assembly;
            if (File.Exists(symbolsPath))
            {
                using FileStream symbolsStream = File.OpenRead(symbolsPath);
                assembly = context.LoadFromStream(
                    assemblyStream,
                    symbolsStream);
            }
            else
            {
                assembly = context.LoadFromStream(assemblyStream);
            }
            Type entryType = assembly.GetType(
                staged.Manifest.EntryType,
                throwOnError: true,
                ignoreCase: false)!;
            if (!typeof(EnginePlugin).IsAssignableFrom(entryType))
            {
                throw new InvalidDataException(
                    $"Plugin entry type {entryType.FullName} does not derive from EnginePlugin. Shared contract identity is broken.");
            }

            EnginePlugin plugin = (EnginePlugin)(Activator.CreateInstance(entryType)
                ?? throw new InvalidOperationException(
                    $"Plugin entry type {entryType.FullName} could not be constructed."));
            scope = new(
                staged.Manifest.Id,
                generation,
                _jobs.CreateGroup(generation));
            if (plugin is IDisposable disposable)
            {
                scope.Own(disposable);
            }

            plugin.Configure(scope);
            ValidateExports(staged.Manifest, scope);
            PluginActivation activation = new(
                generation,
                staged.Manifest,
                context,
                plugin,
                scope);
            context = null!;
            plugin = null!;
            scope = null;
            return activation;
        }
        catch
        {
            if (scope is not null)
            {
                await scope.DisposeAsync();
            }

            context.Unload();
            context = null!;
            scope = null;
            throw;
        }
    }

    private GenerationId NextGeneration()
    {
        GenerationId result = _nextGeneration;
        _nextGeneration = _nextGeneration.Next();
        return result;
    }

    private static void ValidateExports(
        PluginManifest manifest,
        PluginScope scope)
    {
        HashSet<string> exported = scope.Services.Keys
            .Select(type => type.FullName ?? type.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string required in manifest.RequiredExports)
        {
            if (!exported.Contains(required))
            {
                throw new InvalidDataException(
                    $"Plugin {manifest.Id} did not export required service {required}.");
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ReleaseAsync(
        PluginActivation activation)
    {
        PluginLoadContext? context = activation.Context!;
        WeakReference reference = new(context, trackResurrection: false);
        PluginScope? scope = activation.Scope!;
        activation.Plugin = null;
        activation.Scope = null;
        activation.Context = null;
        await scope.DisposeAsync();
        context.Unload();
        scope = null;
        context = null;
        return reference;
    }

    private sealed class PluginActivation(
        GenerationId generation,
        PluginManifest manifest,
        PluginLoadContext context,
        EnginePlugin plugin,
        PluginScope scope)
    {
        public GenerationId Generation { get; } = generation;

        public PluginManifest Manifest { get; } = manifest;

        public PluginLoadContext? Context { get; set; } = context;

        public EnginePlugin? Plugin { get; set; } = plugin;

        public PluginScope? Scope { get; set; } = scope;
    }
}
