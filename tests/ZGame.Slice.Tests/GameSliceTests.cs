using System.Diagnostics;
using ZEngine.Plugins.Runtime;
using ZEngine.UI;
using ZGame.Slice.Contracts;
using ZGame.Slice.UI;
using Xunit;

namespace ZGame.Slice.Tests;

public sealed class GameSliceTests
{
    [Fact]
    public async Task RealSliceIntegratesPluginsAssetsAndDependentReload()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string cache = TemporaryDirectory();
        try
        {
            await using GameSliceRuntime runtime = await GameSliceRuntime.CreateAsync(
                cache,
                HostContentRoot(),
                cancellation);
            Assert.Equal(
                ["game.gameplay", "game.render", "game.director"],
                runtime.PluginIds);
            Assert.Equal(3, runtime.AssetResidency.Count);
            Assert.All(runtime.AssetResidency, resident =>
            {
                Assert.Equal(1, resident.References);
                Assert.True(resident.Bytes > 0);
            });

            GameFrame frame = runtime.Tick(1f / 60);
            Assert.Equal(50_000, frame.Gameplay.EntityCount);
            Assert.Equal(5, frame.RenderPassCount);
            Assert.True(frame.PresentedFrame > 0);
            PluginSetReloadResult reload = await runtime.ReloadGameplayAsync(cancellation);
            Assert.Equal(PluginReloadStatus.Applied, reload.Status);
            Assert.Equal(
                ["game.gameplay", "game.render", "game.director"],
                reload.ReloadedPlugins);
            Assert.Equal(50_000, runtime.Tick(1f / 60).Gameplay.EntityCount);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public void HudAndMenuAreReactiveStronglyTypedUi()
    {
        using GameHud hud = new();
        hud.Update(new(
            new(12, 50_000, 87.5f, 1, 2),
            5,
            99));
        GameTheme theme = new();
        UiTree before = hud.Render(theme);
        UiNode button = Flatten(before.Root).Single(node =>
            node.SemanticLabel == "切换菜单");
        Assert.Contains(Flatten(before.Root), node => node.SemanticLabel == "主菜单");
        Assert.Contains(Flatten(before.Root), node => node.Text == "实体 50,000");
        Assert.Contains(Flatten(before.Root), node => node.Text == "生命 87.5");

        var click = (UiEventHandler<UiClickEvent>)button.Events[UiEventType.Click];
        click(new(button.Id));
        UiTree after = hud.Render(theme);
        Assert.DoesNotContain(Flatten(after.Root), node => node.SemanticLabel == "主菜单");
        Assert.Equal(button.Id, Flatten(after.Root).Single(node =>
            node.SemanticLabel == "切换菜单").Id);
        Assert.True(after.Change.Removed > 0);
    }

    [Fact]
    public async Task FiftyThousandEntitySliceStaysInsideCpuBudget()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string cache = TemporaryDirectory();
        try
        {
            await using GameSliceRuntime runtime = await GameSliceRuntime.CreateAsync(
                cache,
                HostContentRoot(),
                cancellation);
            for (int index = 0; index < 100; index++)
            {
                runtime.Tick(1f / 60);
            }

            const int frames = 2_000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch timer = Stopwatch.StartNew();
            for (int index = 0; index < frames; index++)
            {
                runtime.Tick(1f / 60);
            }

            timer.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            double millisecondsPerFrame = timer.Elapsed.TotalMilliseconds / frames;
            Assert.True(
                millisecondsPerFrame < 2.5,
                $"Gameplay + render graph took {millisecondsPerFrame:0.000} ms/frame.");
            Assert.InRange(allocated, 0, 1_024);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    private static string TemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "zengine-slice-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string HostContentRoot()
    {
        string repository = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
        return Path.Combine(
            repository,
            "samples",
            "ZGame.Slice",
            "bin",
            "Release",
            "net11.0");
    }

    private static IEnumerable<UiNode> Flatten(UiNode node)
    {
        yield return node;
        foreach (UiNode child in node.Children)
        {
            foreach (UiNode descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }
}
