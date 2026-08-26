using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

#pragma warning disable ASP0006 // Dynamic IR nodes require deterministic runtime sequence assignment.

namespace ZEngine.UI.Blazor;

public sealed class BlazorUiRenderer
{
    public RenderFragment Render(UiTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return builder =>
        {
            int sequence = 0;
            foreach (UiNode child in tree.Root.Children)
            {
                BuildNode(builder, child, ref sequence);
            }
        };
    }

    public void Build(
        RenderTreeBuilder builder,
        UiTree tree,
        object? eventReceiver = null,
        Action? onEvent = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tree);
        int sequence = 0;
        foreach (UiNode child in tree.Root.Children)
        {
            BuildNode(
                builder,
                child,
                ref sequence,
                eventReceiver,
                onEvent);
        }
    }

    private static void BuildNode(
        RenderTreeBuilder builder,
        UiNode node,
        ref int sequence,
        object? eventReceiver = null,
        Action? onEvent = null)
    {
        if (node.Tag == UiTag.Text)
        {
            builder.AddContent(sequence++, node.Text);
            return;
        }

        builder.OpenElement(sequence++, ToElementName(node.Tag));
        builder.SetKey(node.Id.Value);
        builder.AddAttribute(sequence++, "data-zui-id", node.Id.ToString());
        string style = CssStyleCompiler.Compile(node.Style);
        if (style.Length > 0)
        {
            builder.AddAttribute(sequence++, "style", style);
        }

        if (node.Role != UiRole.None)
        {
            builder.AddAttribute(sequence++, "role", ToRole(node.Role));
        }

        if (node.SemanticLabel is not null)
        {
            builder.AddAttribute(sequence++, "aria-label", node.SemanticLabel);
        }

        if (node.Value is not null)
        {
            builder.AddAttribute(sequence++, "value", node.Value);
        }

        if (node.Placeholder is not null)
        {
            builder.AddAttribute(sequence++, "placeholder", node.Placeholder);
        }

        if (node.Events.TryGetValue(UiEventType.Click, out Delegate? click))
        {
            var handler = (UiEventHandler<UiClickEvent>)click;
            builder.AddAttribute(
                sequence++,
                "onclick",
                EventCallback.Factory.Create<MouseEventArgs>(
                    eventReceiver ?? node,
                    () =>
                    {
                        handler(new(node.Id));
                        onEvent?.Invoke();
                    }));
        }

        if (node.Events.TryGetValue(UiEventType.Input, out Delegate? input))
        {
            var handler = (UiEventHandler<UiInputEvent>)input;
            builder.AddAttribute(
                sequence++,
                "oninput",
                EventCallback.Factory.Create<ChangeEventArgs>(
                    eventReceiver ?? node,
                    args =>
                    {
                        handler(new(
                            node.Id,
                            args.Value?.ToString() ?? string.Empty));
                        onEvent?.Invoke();
                    }));
        }

        if (node.Text is not null)
        {
            builder.AddContent(sequence++, node.Text);
        }

        foreach (UiNode child in node.Children)
        {
            BuildNode(
                builder,
                child,
                ref sequence,
                eventReceiver,
                onEvent);
        }

        builder.CloseElement();
    }

    private static string ToElementName(UiTag tag) => tag switch
    {
        UiTag.Div => "div",
        UiTag.Span => "span",
        UiTag.Heading1 => "h1",
        UiTag.Button => "button",
        UiTag.Input => "input",
        _ => "div"
    };

    private static string ToRole(UiRole role) => role switch
    {
        UiRole.Group => "group",
        UiRole.Heading => "heading",
        UiRole.Button => "button",
        UiRole.TextBox => "textbox",
        UiRole.Label => "label",
        UiRole.Status => "status",
        _ => "none"
    };
}

public static class CssStyleCompiler
{
    public static string Compile(UiStyle style)
    {
        StringBuilder css = new();
        Add("display", style.Display switch
        {
            UiDisplay.Block => "block",
            UiDisplay.Flex => "flex",
            UiDisplay.Grid => "grid",
            UiDisplay.None => "none",
            _ => "block"
        });
        if (style.Display == UiDisplay.Flex)
        {
            Add("flex-direction", style.FlexDirection == FlexDirection.Row
                ? "row"
                : "column");
            Add("align-items", ToKebab(style.AlignItems.ToString()));
            Add("justify-content", ToKebab(style.JustifyContent.ToString()));
        }

        AddLength("width", style.Width);
        AddLength("height", style.Height);
        AddLength("gap", style.Gap);
        AddEdges("padding", style.Padding);
        AddEdges("margin", style.Margin);
        if (style.BackgroundColor is { } background)
        {
            Add("background-color", background.ToCss());
        }

        if (style.Color is { } foreground)
        {
            Add("color", foreground.ToCss());
        }

        if (style.BorderColor is { } border)
        {
            Add("border-color", border.ToCss());
            Add("border-style", "solid");
        }

        AddLength("border-width", style.BorderWidth);
        AddLength("border-radius", style.BorderRadius);
        Add("font-size", $"{style.FontSize.ToString("0.###", CultureInfo.InvariantCulture)}px");
        Add("font-weight", style.FontWeight.ToString(CultureInfo.InvariantCulture));
        if (style.Opacity != 1)
        {
            Add("opacity", style.Opacity.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (style.Shadow is { } shadow)
        {
            Add(
                "box-shadow",
                FormattableString.Invariant(
                    $"{shadow.OffsetX:0.###}px {shadow.OffsetY:0.###}px {shadow.Blur:0.###}px {shadow.Spread:0.###}px {shadow.Color.ToCss()}"));
        }

        return css.ToString();

        void Add(string name, string value)
        {
            css.Append(name).Append(':').Append(value).Append(';');
        }

        void AddLength(string name, UiLength value)
        {
            if (value.Unit != LengthUnit.Auto)
            {
                Add(name, value.ToCss());
            }
        }

        void AddEdges(string name, UiEdges edges) =>
            Add(
                name,
                $"{edges.Top.ToCss()} {edges.Right.ToCss()} {edges.Bottom.ToCss()} {edges.Left.ToCss()}");
    }

    private static string ToKebab(string value)
    {
        StringBuilder result = new();
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                result.Append('-');
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }
}

public sealed class ZuiHostComponent : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required UiTree Tree { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        new BlazorUiRenderer().Build(
            builder,
            Tree,
            this,
            StateHasChanged);
}

#pragma warning restore ASP0006
