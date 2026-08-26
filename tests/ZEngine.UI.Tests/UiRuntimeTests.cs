using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UiLab.Shared;
using Xunit;
using ZEngine.UI.Blazor;
using ZEngine.UI.Rendering;
using ZEngine.UI.Windows;

namespace ZEngine.UI.Tests;

public sealed class UiRuntimeTests
{
    [Fact]
    public void OneDslBuildsTypedTreeReactsAndReusesStableFrame()
    {
        using LoginCard component = new();
        AppTheme theme = new();
        UiTree first = component.Render(theme);
        UiNode input = Find(first.Root, UiTag.Input);
        UiNode button = Find(first.Root, UiTag.Button);

        Assert.Equal("用户名", input.SemanticLabel);
        Assert.Equal(UiRole.Button, button.Role);
        Assert.Equal(theme.Colors.Primary, button.Style.BackgroundColor);
        ((UiEventHandler<UiInputEvent>)input.Events[UiEventType.Input])(
            new(input.Id, "狮心"));
        UiTree second = component.Render(theme);
        Assert.NotSame(first, second);
        Assert.Equal(0, second.Change.Added);
        Assert.Equal(0, second.Change.Removed);
        Assert.True(second.Change.Updated > 0);
        Assert.Contains("狮心", FlattenText(second.Root));

        UiTree stable = component.Render(theme);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 10_000; frame++)
        {
            Assert.Same(stable, component.Render(theme));
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public async Task BlazorBackendRendersRealDomAndTypedCss()
    {
        using LoginCard component = new();
        UiTree tree = component.Render(new());
        ServiceCollection services = new();
        services.AddLogging();
        await using HtmlRenderer renderer = new(
            services.BuildServiceProvider(),
            LoggerFactory.Create(static _ => { }));
        string html = string.Empty;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<ZuiHostComponent>(
                ParameterView.FromDictionary(
                    new Dictionary<string, object?>
                    {
                        [nameof(ZuiHostComponent.Tree)] = tree
                    }));
            html = output.ToHtmlString();
        });

        string decoded = WebUtility.HtmlDecode(html);

        Assert.Contains("<div", html);
        Assert.Contains("<input", html);
        Assert.Contains("<button", html);
        Assert.Contains("欢迎回来", decoded);
        Assert.Contains("display:flex", html);
        Assert.Contains("border-radius:16px", html);
        Assert.DoesNotContain("&lt;div", html);
    }

    [Fact]
    public void NativeLayoutProducesBoxesTextAndChineseSystemFont()
    {
        using LoginCard component = new();
        UiTree tree = component.Render(new());
        UiLayout layout = new UiLayoutEngine().Layout(tree, 1280, 720);
        IReadOnlyList<UiPaintCommand> paint =
            new UiPaintCompiler().Compile(tree, layout);

        Assert.Contains(paint, command => command is PaintBox);
        Assert.Contains(
            paint,
            command => command is PaintText text && text.Text.Contains("中文"));
        Assert.All(layout.Bounds.Values, bounds =>
        {
            Assert.True(bounds.Width >= 0);
            Assert.True(bounds.Height >= 0);
        });

        if (OperatingSystem.IsWindows())
        {
            SystemFontCatalog catalog = new();
            SystemFontFace face = catalog.Resolve("中文 ZEngine");
            Assert.True(face.Supports("中文 ZEngine"));
            GlyphAtlas atlas = new(width: 128, height: 128, cellSize: 64);
            GlyphSlot slot = atlas.GetOrAdd(new(
                face.Path,
                face.FaceOffset,
                '中',
                32));
            Assert.True(slot.Generation > 0);
            using GdiGlyphRasterizer rasterizer = new();
            RasterizedGlyph glyph = rasterizer.Rasterize(
                Rune.GetRuneAt("中", 0),
                28,
                400);
            Assert.True(glyph.Width > 0);
            Assert.Contains(glyph.Alpha, alpha => alpha > 0);
        }
    }

    private static UiNode Find(UiNode node, UiTag tag)
    {
        if (node.Tag == tag)
        {
            return node;
        }

        foreach (UiNode child in node.Children)
        {
            UiNode? found = FindOrNull(child, tag);
            if (found is not null)
            {
                return found;
            }
        }

        throw new InvalidOperationException($"UI node {tag} was not found.");
    }

    private static UiNode? FindOrNull(UiNode node, UiTag tag)
    {
        if (node.Tag == tag)
        {
            return node;
        }

        foreach (UiNode child in node.Children)
        {
            UiNode? found = FindOrNull(child, tag);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string FlattenText(UiNode node) =>
        (node.Text ?? string.Empty)
        + string.Concat(node.Children.Select(FlattenText));
}
