using System.Runtime.CompilerServices;
using ZEngine.ChainPlugin.Contracts;
using ZEngine.Plugins.Runtime;
using Xunit;

namespace ZEngine.Plugins.Tests;

public sealed class PluginSetRuntimeTests
{
    [Fact]
    public async Task AtomicallyReloadsRequiredClosureWithoutReloadingIndependentPlugin()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string root = Path.Combine(
            Path.GetTempPath(),
            "zengine-plugin-set-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        PluginSetRuntime? runtime = null;
        try
        {
            runtime = new(root, workerCount: 2);
            PluginPackage[] packages = ChainPackages();
            await runtime.LoadInitialAsync(packages, cancellation);
            IChainA oldA = runtime.GetService<IChainA>();
            IChainB oldB = runtime.GetService<IChainB>();
            IChainC oldC = runtime.GetService<IChainC>();
            IChainD oldD = runtime.GetService<IChainD>();

            PluginSetReloadResult applied = await runtime.ReloadClosureAsync(
                "A",
                packages.Single(package => package.Manifest.Id == "A"),
                cancellationToken: cancellation);
            Assert.Equal(PluginReloadStatus.Applied, applied.Status);
            Assert.Equal(["A", "B", "C"], applied.ReloadedPlugins);
            IChainA newA = runtime.GetService<IChainA>();
            IChainB newB = runtime.GetService<IChainB>();
            IChainC newC = runtime.GetService<IChainC>();
            Assert.NotSame(oldA, newA);
            Assert.NotSame(oldB, newB);
            Assert.NotSame(oldC, newC);
            Assert.Same(newA, newB.Provider);
            Assert.Same(newB, newC.Provider);
            Assert.Same(oldD, runtime.GetService<IChainD>());
            Assert.True(oldA.IsDisposed);
            Assert.True(oldB.IsDisposed);
            Assert.True(oldC.IsDisposed);
            Assert.False(oldD.IsDisposed);

            PluginSetReloadResult rolledBack = await runtime.ReloadClosureAsync(
                "A",
                packages.Single(package => package.Manifest.Id == "A"),
                probation: static _ => false,
                cancellationToken: cancellation);
            Assert.Equal(PluginReloadStatus.RolledBack, rolledBack.Status);
            Assert.Same(newA, runtime.GetService<IChainA>());
            Assert.Same(newB, runtime.GetService<IChainB>());
            Assert.Same(newC, runtime.GetService<IChainC>());
            Assert.False(newA.IsDisposed);

            PluginPackage invalid = packages.Single(package =>
                package.Manifest.Id == "A") with
            {
                Manifest = packages[0].Manifest with
                {
                    EntryType = "ZEngine.ChainPlugin.Runtime.Missing"
                }
            };
            PluginSetReloadResult rejected = await runtime.ReloadClosureAsync(
                "A",
                invalid,
                cancellationToken: cancellation);
            Assert.Equal(PluginReloadStatus.Rejected, rejected.Status);
            Assert.Same(newA, runtime.GetService<IChainA>());
            Assert.Same(oldD, runtime.GetService<IChainD>());

            oldA = null!;
            oldB = null!;
            oldC = null!;
            oldD = null!;
            newA = null!;
            newB = null!;
            newC = null!;
            await runtime.DisposeAsync();
            runtime = null;
            ForceCollection();
        }
        finally
        {
            if (runtime is not null)
            {
                await runtime.DisposeAsync();
            }

            Directory.Delete(root, recursive: true);
        }
    }

    private static PluginPackage[] ChainPackages()
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string source = Path.Combine(
            repositoryRoot,
            "tests",
            "Fixtures",
            "ZEngine.ChainPlugin.Runtime",
            "bin",
            "Debug",
            "net11.0");
        return
        [
            Package<IChainA>("A", "PluginA", source),
            Package<IChainB>(
                "B",
                "PluginB",
                source,
                new PluginDependency("A", "[1.0.0,2.0.0)")),
            Package<IChainC>(
                "C",
                "PluginC",
                source,
                new PluginDependency("B", "[1.0.0,2.0.0)")),
            Package<IChainD>("D", "PluginD", source)
        ];
    }

    private static PluginPackage Package<TService>(
        string id,
        string typeName,
        string source,
        params PluginDependency[] dependencies)
        where TService : class =>
        new(
            new(
                id,
                "1.0.0",
                "ZEngine.ChainPlugin.Runtime.dll",
                "ZEngine.ChainPlugin.Runtime." + typeName,
                ["ZEngine.ChainPlugin.Contracts"],
                dependencies,
                [typeof(TService).FullName!]),
            source);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
