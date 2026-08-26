using System.Runtime.CompilerServices;
using ZEngine.CounterPlugin.Contracts;
using ZEngine.Core;
using ZEngine.Plugins.Runtime;
using Xunit;

namespace ZEngine.Plugins.Tests;

public sealed class PluginRuntimeTests
{
    [Fact]
    public async Task AppliesStatefulReloadRejectsStagingFailureAndRollsBackProbation()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string generationRoot = NewGenerationRoot();
        PluginRuntime? runtime = null;
        try
        {
            runtime = new(generationRoot);
            PluginPackage package = CounterPackage();
            TransactionEvidence evidence = ExerciseTransactions(
                runtime,
                package,
                cancellation);
            DisposeRuntime(runtime);
            WeakReference[] allContexts = runtime.UnloadedContexts.ToArray();
            runtime = null;
            ForceCollection();
            Assert.All(allContexts, reference => Assert.False(reference.IsAlive));
            Assert.False(evidence.AppliedContext.IsAlive);
            Assert.False(evidence.RolledBackContext.IsAlive);
        }
        finally
        {
            if (runtime is not null)
            {
                DisposeRuntime(runtime);
                runtime = null;
            }

            await DeleteGenerationRootAsync(generationRoot, cancellation);
        }
    }

    [Fact]
    public async Task ReloadWaitsForPluginJobThenDisposesScopedService()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string generationRoot = NewGenerationRoot();
        PluginRuntime? runtime = null;
        try
        {
            runtime = new(generationRoot);
            PluginPackage package = CounterPackage();
            await runtime.LoadInitialAsync(package, cancellation);
            ICounterService oldService = runtime.GetService<ICounterService>();
            oldService.StartBlockingWork();
            await oldService.WorkStarted.WaitAsync(cancellation);

            Task<PluginReloadResult> reload = runtime.ReloadAsync(
                package,
                cancellationToken: cancellation);
            await Task.Delay(100, cancellation);
            Assert.False(reload.IsCompleted);
            oldService.ReleaseWork();
            PluginReloadResult result = await reload;

            Assert.Equal(PluginReloadStatus.Applied, result.Status);
            Assert.True(oldService.IsDisposed);
            oldService = null!;
            DisposeRuntime(runtime);
            runtime = null;
            ForceCollection();
        }
        finally
        {
            if (runtime is not null)
            {
                DisposeRuntime(runtime);
                runtime = null;
            }

            await DeleteGenerationRootAsync(generationRoot, cancellation);
        }
    }

    [Fact]
    public async Task ReloadsOneThousandCollectibleGenerationsWithoutAlcGrowth()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string generationRoot = NewGenerationRoot();
        long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        PluginRuntime? runtime = null;
        try
        {
            runtime = new(generationRoot);
            PluginPackage package = CounterPackage();
            ReloadMany(runtime, package, cancellation);
            DisposeRuntime(runtime);
            WeakReference[] references = runtime.UnloadedContexts.ToArray();
            runtime = null;
            ForceCollection();
            Assert.All(references, reference => Assert.False(reference.IsAlive));
            long retained = GC.GetTotalMemory(forceFullCollection: true) - memoryBefore;
            Assert.True(
                retained < 32 * 1024 * 1024,
                $"1,000 reloads retained {retained:N0} managed bytes.");
        }
        finally
        {
            if (runtime is not null)
            {
                DisposeRuntime(runtime);
            }

            await DeleteGenerationRootAsync(generationRoot, cancellation);
        }
    }

    private static PluginPackage CounterPackage()
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string sourceDirectory = Path.Combine(
            repositoryRoot,
            "tests",
            "Fixtures",
            "ZEngine.CounterPlugin.Runtime",
            "bin",
            "Debug",
            "net11.0");
        PluginManifest manifest = new(
            "zengine.tests.counter",
            "1.0.0",
            "ZEngine.CounterPlugin.Runtime.dll",
            "ZEngine.CounterPlugin.Runtime.CounterPlugin",
            ["ZEngine.CounterPlugin.Contracts"],
            [],
            [typeof(ICounterService).FullName!]);
        return new(manifest, sourceDirectory);
    }

    private static string NewGenerationRoot()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "zengine-plugin-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForceCollection()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReloadMany(
        PluginRuntime runtime,
        PluginPackage package,
        CancellationToken cancellationToken)
    {
        runtime.LoadInitialAsync(package, cancellationToken)
            .GetAwaiter()
            .GetResult();
        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            ICounterService service = runtime.GetService<ICounterService>();
            service.Value = iteration;
            service = null!;
            PluginReloadResult result = runtime.ReloadAsync(
                    package,
                    cancellationToken: cancellationToken)
                .GetAwaiter()
                .GetResult();
            Assert.Equal(PluginReloadStatus.Applied, result.Status);
            result = null!;
        }

        ICounterService current = runtime.GetService<ICounterService>();
        Assert.Equal(999, current.Value);
        current = null!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TransactionEvidence ExerciseTransactions(
        PluginRuntime runtime,
        PluginPackage package,
        CancellationToken cancellationToken)
    {
        runtime.LoadInitialAsync(package, cancellationToken)
            .GetAwaiter()
            .GetResult();
        ICounterService initial = runtime.GetService<ICounterService>();
        initial.Value = 41;
        PluginReloadResult applied = runtime.ReloadAsync(
                package,
                cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();
        Assert.Equal(PluginReloadStatus.Applied, applied.Status);
        ICounterService reloaded = runtime.GetService<ICounterService>();
        Assert.NotSame(initial, reloaded);
        Assert.True(initial.IsDisposed);
        Assert.Equal(41, reloaded.Value);

        GenerationId active = runtime.CurrentGeneration;
        PluginManifest invalidManifest = package.Manifest with
        {
            EntryType = "ZEngine.CounterPlugin.Runtime.MissingPlugin"
        };
        PluginReloadResult rejected = runtime.ReloadAsync(
                package with { Manifest = invalidManifest },
                cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();
        Assert.Equal(PluginReloadStatus.Rejected, rejected.Status);
        Assert.Equal(active, runtime.CurrentGeneration);
        Assert.Same(reloaded, runtime.GetService<ICounterService>());

        PluginReloadResult rolledBack = runtime.ReloadAsync(
                package,
                probation: static _ => false,
                cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();
        Assert.Equal(PluginReloadStatus.RolledBack, rolledBack.Status);
        Assert.Equal(active, runtime.CurrentGeneration);
        Assert.Same(reloaded, runtime.GetService<ICounterService>());
        Assert.False(reloaded.IsDisposed);
        TransactionEvidence evidence = new(
            applied.UnloadedContext!,
            rolledBack.UnloadedContext!);
        initial = null!;
        reloaded = null!;
        applied = null!;
        rejected = null!;
        rolledBack = null!;
        return evidence;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DisposeRuntime(PluginRuntime runtime) =>
        runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();

    private sealed record TransactionEvidence(
        WeakReference AppliedContext,
        WeakReference RolledBackContext);

    private static async Task DeleteGenerationRootAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            ForceCollection();
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastError = exception;
                await Task.Delay(50, cancellationToken);
            }
        }

        throw new IOException(
            $"Collectible plugin files remained locked after unload: {path}",
            lastError);
    }
}
