using ZEngine.UI;
using ZEngine.UI.Basic;

namespace ZEngine.Editor;

public sealed class EditorTheme : SysTheme
{
    public override ThemeColors Colors { get; } = new(
        UiColor.FromRgb(0x0B0E14),
        UiColor.FromRgb(0x171C26),
        UiColor.FromRgb(0x6588FF),
        UiColor.FromRgb(0xFFFFFF),
        UiColor.FromRgb(0xE8ECF5),
        UiColor.FromRgb(0x98A3B7),
        UiColor.FromRgb(0x30394A),
        UiColor.FromRgb(0xFF6B6B));
}

public sealed class EditorShell(
    EditorPanelRegistry panels,
    EditorContext context) : Component<EditorTheme>
{
    private readonly State<ulong> _revision = new(0);

    public void Refresh() => _revision.Value++;

    protected override void Compose(Ui<EditorTheme> ui)
    {
        _ = _revision.Value;
        IReadOnlyList<IEditorPanel> snapshot = panels.Snapshot();
        ui.DIV(page =>
        {
            page.Semantics(UiRole.Group, "ZEngine 编辑器");
            page.BackgroundColor.Page();
            page.Color.Text();
            page.Padding.All(12);
            page.Gap.Px(10);
            page.Width.Full();
            page.H1("ZEngine Editor · Vulkan 1.4", heading =>
            {
                heading.Semantics(UiRole.Heading, "ZEngine Editor");
                heading.Typography.Size(22).Weight(700);
            });
            page.DIV(toolbar =>
            {
                toolbar.Flex.Row().Align(AlignItems.Center);
                toolbar.Gap.Px(8);
                toolbar.BUTTON("▶ 独立进程运行", button =>
                {
                    button.BackgroundColor.Primary();
                    button.Color.PrimaryText();
                    button.Padding.XY(12, 6);
                    button.Border.Radius(6);
                    button.Semantics(UiRole.Button, "运行游戏");
                });
                toolbar.TEXT("游戏崩溃不会关闭编辑器");
            });
            page.DIV(workspace =>
            {
                workspace.Flex.Row().Align(AlignItems.Start);
                workspace.Gap.Px(10);
                ComposeColumn(workspace, snapshot, "scene", "assets", "plugins");
                ComposeColumn(workspace, snapshot, "viewport", "render-graph", "console");
                ComposeColumn(workspace, snapshot, "inspector", "ecs", "reloads", "gpu");
            });
        });
    }

    private void ComposeColumn(
        UiElement<EditorTheme> workspace,
        IReadOnlyList<IEditorPanel> snapshot,
        params string[] ids)
    {
        workspace.DIV(column =>
        {
            column.Width.Px(ids.Contains("viewport", StringComparer.Ordinal) ? 430 : 260);
            column.Gap.Px(8);
            foreach (string id in ids)
            {
                IEditorPanel? panel = snapshot.FirstOrDefault(candidate => candidate.Id == id);
                if (panel is null)
                {
                    continue;
                }

                column.DIV(card =>
                {
                    card.Semantics(UiRole.Group, panel.Title);
                    card.BackgroundColor.Surface();
                    card.Color.Text();
                    card.Padding.All(10);
                    card.Gap.Px(4);
                    card.Border.Width(1).Radius(8).DefaultColor();
                    card.H1(panel.Title, title => title.Typography.Size(16).Weight(700));
                    panel.Compose(card, context);
                }, key: panel.Id);
            }
        }, key: string.Join('-', ids));
    }
}
