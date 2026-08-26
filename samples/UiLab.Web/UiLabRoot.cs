using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using UiLab.Shared;
using ZEngine.UI.Blazor;

namespace UiLab.Web;

public sealed class UiLabRoot : ComponentBase, IDisposable
{
    private readonly LoginCard _loginCard = new();
    private readonly AppTheme _theme = new();
    private readonly BlazorUiRenderer _renderer = new();

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        _renderer.Build(
            builder,
            _loginCard.Render(_theme),
            this,
            StateHasChanged);

    public void Dispose() => _loginCard.Dispose();
}
