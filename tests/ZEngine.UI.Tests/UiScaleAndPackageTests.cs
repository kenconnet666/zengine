using UiLab.Shared;
using Xunit;
using ZEngine.UI;
using ZEngine.UI.Basic;
using ZEngine.UI.Sdk;
using ZEngine.Core;
using ZEngine.Jobs;
using ZEngine.Plugins;

namespace ZEngine.UI.Tests;

public sealed class UiScaleAndPackageTests
{
    [Fact]
    public void TenThousandComponentsRecomposeOnlyDirtyItem()
    {
        ItemComponent[] items = Enumerable.Range(0, 10_000)
            .Select(index => new ItemComponent(index))
            .ToArray();
        AppTheme theme = new();
        UiComponentSurface<AppTheme> surface = new(items);
        UiSurfaceUpdate initial = surface.Update(theme);
        Assert.Equal(10_000, initial.ChangedComponents.Count);

        items[4_321].Value.Value = 99_999;
        UiSurfaceUpdate local = surface.Update(theme);

        Assert.Equal([4_321], local.ChangedComponents);
        Assert.Equal(9_999, local.ReusedComponents);
        Assert.Equal(2, items[4_321].ComposeCount);
        Assert.All(items.Where((_, index) => index != 4_321), item =>
            Assert.Equal(1, item.ComposeCount));
    }

    [Fact]
    public void PrimaryRendererConflictIsExplicitAndRegistrationIsReversible()
    {
        UiEnhancementRegistry registry = new();
        UiRenderKey key = new(UiBackend.Native, "Zui.Basic.Button");
        IDisposable basic = registry.RegisterPrimaryRenderer(
            "zui.basic",
            key,
            (Func<object>)(static () => new object()));

        UiPackageConflictException conflict = Assert.Throws<UiPackageConflictException>(() =>
            registry.RegisterPrimaryRenderer(
                "zui.windows.fluent",
                key,
                (Func<object>)(static () => new object())));
        Assert.Equal("ZUI4102", conflict.DiagnosticId);
        Assert.Contains("zui.basic", conflict.Message);
        Assert.Contains("zui.windows.fluent", conflict.Message);

        basic.Dispose();
        using IDisposable replacement = registry.RegisterPrimaryRenderer(
            "zui.windows.fluent",
            key,
            (Func<object>)(static () => new object()));
        Assert.Single(registry.Renderers);
    }

    [Fact]
    public async Task PluginScopeDisposalRemovesEnhancementRegistration()
    {
        using JobScheduler jobs = new(workerCount: 1);
        JobGroup group = jobs.CreateGroup(GenerationId.Initial);
        PluginScope scope = new("zui.tests.enhancement", GenerationId.Initial, group);
        UiEnhancementRegistry registry = new();
        IDisposable registration = registry.RegisterPrimaryRenderer(
            "zui.tests.enhancement",
            new(UiBackend.Native, "Test.Panel"),
            (Func<object>)(static () => new object()));
        scope.Own(registration);
        Assert.Single(registry.Renderers);

        await scope.DisposeAsync();

        Assert.Empty(registry.Renderers);
    }

    private sealed class ItemComponent(int value) : Component<AppTheme>
    {
        public State<int> Value { get; } = new(value);

        public int ComposeCount { get; private set; }

        protected override void Compose(Ui<AppTheme> ui)
        {
            ComposeCount++;
            ui.DIV(
                Value.Value,
                static (row, item) => row.TEXT(item.ToString()),
                key: "item");
        }
    }
}
