using System.Runtime.CompilerServices;

namespace ZEngine.UI.Basic;

public delegate void UiBuild<TTheme>(UiElement<TTheme> element)
    where TTheme : SysTheme;

public delegate void UiBuild<TTheme, in TModel>(
    UiElement<TTheme> element,
    TModel model)
    where TTheme : SysTheme;

public sealed class Ui<TTheme>
    where TTheme : SysTheme
{
    private readonly UiComposer<TTheme> _composer;

    public Ui(UiComposer<TTheme> composer)
    {
        _composer = composer;
    }

    public void DIV(
        UiBuild<TTheme> build,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0) =>
        Build(UiTag.Div, build, key, callerFile, callerLine);

    public void DIV<TModel>(
        TModel model,
        UiBuild<TTheme, TModel> build,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        ArgumentNullException.ThrowIfNull(build);
        UiNode node = _composer.Add(
            UiTag.Div,
            key,
            callerFile,
            callerLine);
        using (_composer.Enter(node))
        {
            build(new(_composer, node), model);
        }
    }

    public void TEXT(
        string text,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        _composer.Add(UiTag.Text, key, callerFile, callerLine).Text = text;
    }

    private void Build(
        UiTag tag,
        UiBuild<TTheme> build,
        string? key,
        string callerFile,
        int callerLine)
    {
        ArgumentNullException.ThrowIfNull(build);
        UiNode node = _composer.Add(tag, key, callerFile, callerLine);
        using (_composer.Enter(node))
        {
            build(new(_composer, node));
        }
    }
}

public readonly struct UiElement<TTheme>
    where TTheme : SysTheme
{
    private readonly UiComposer<TTheme> _composer;
    private readonly UiNode _node;

    internal UiElement(UiComposer<TTheme> composer, UiNode node)
    {
        _composer = composer;
        _node = node;
    }

    public UiNodeId Id => _node.Id;

    public BackgroundColorStyle<TTheme> BackgroundColor =>
        new(_node.Style, _composer.Theme);

    public ForegroundColorStyle<TTheme> Color =>
        new(_node.Style, _composer.Theme);

    public LengthStyle Width => new(
        _node.Style,
        LengthProperty.Width);

    public LengthStyle Height => new(
        _node.Style,
        LengthProperty.Height);

    public EdgeStyle Padding => new(
        _node.Style,
        EdgeProperty.Padding);

    public EdgeStyle Margin => new(
        _node.Style,
        EdgeProperty.Margin);

    public LengthStyle Gap => new(
        _node.Style,
        LengthProperty.Gap);

    public BorderStyle<TTheme> Border =>
        new(_node.Style, _composer.Theme);

    public ShadowStyle Shadow => new(_node.Style);

    public DisplayStyle Display => new(_node.Style);

    public FlexStyle Flex => new(_node.Style);

    public TypographyStyle Typography => new(_node.Style);

    public UiElement<TTheme> Semantics(
        UiRole role,
        string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _node.Role = role;
        _node.SemanticLabel = label;
        return this;
    }

    public UiElement<TTheme> OnClick(UiEventHandler<UiClickEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _node.Events[UiEventType.Click] = handler;
        return this;
    }

    public UiElement<TTheme> OnInput(UiEventHandler<UiInputEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _node.Events[UiEventType.Input] = handler;
        return this;
    }

    public UiElement<TTheme> Value(string value)
    {
        _node.Value = value;
        return this;
    }

    public UiElement<TTheme> Placeholder(string value)
    {
        _node.Placeholder = value;
        return this;
    }

    public void DIV(
        UiBuild<TTheme> build,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0) =>
        Build(UiTag.Div, build, key, callerFile, callerLine);

    public void DIV<TModel>(
        TModel model,
        UiBuild<TTheme, TModel> build,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        ArgumentNullException.ThrowIfNull(build);
        UiNode node = _composer.Add(
            UiTag.Div,
            key,
            callerFile,
            callerLine);
        using (_composer.Enter(node))
        {
            build(new(_composer, node), model);
        }
    }

    public void SPAN(
        UiBuild<TTheme> build,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0) =>
        Build(UiTag.Span, build, key, callerFile, callerLine);

    public void H1(
        string text,
        UiBuild<TTheme>? build = null,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0) =>
        BuildText(UiTag.Heading1, text, build, key, callerFile, callerLine);

    public void BUTTON(
        string text,
        UiBuild<TTheme>? build = null,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0) =>
        BuildText(UiTag.Button, text, build, key, callerFile, callerLine);

    public void INPUT(
        UiBuild<TTheme> build,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0) =>
        Build(UiTag.Input, build, key, callerFile, callerLine);

    public void TEXT(
        string text,
        string? key = null,
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        _composer.Add(UiTag.Text, key, callerFile, callerLine).Text = text;
    }

    private void BuildText(
        UiTag tag,
        string text,
        UiBuild<TTheme>? build,
        string? key,
        string callerFile,
        int callerLine)
    {
        ArgumentNullException.ThrowIfNull(text);
        UiNode node = _composer.Add(tag, key, callerFile, callerLine);
        node.Text = text;
        if (build is not null)
        {
            using (_composer.Enter(node))
            {
                build(new(_composer, node));
            }
        }
    }

    private void Build(
        UiTag tag,
        UiBuild<TTheme> build,
        string? key,
        string callerFile,
        int callerLine)
    {
        ArgumentNullException.ThrowIfNull(build);
        UiNode node = _composer.Add(tag, key, callerFile, callerLine);
        using (_composer.Enter(node))
        {
            build(new(_composer, node));
        }
    }
}
