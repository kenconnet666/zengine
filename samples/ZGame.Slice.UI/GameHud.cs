using ZEngine.UI;
using ZEngine.UI.Basic;
using ZGame.Slice.Contracts;

namespace ZGame.Slice.UI;

public sealed class GameTheme : SysTheme
{
    public override ThemeColors Colors { get; } = new(
        UiColor.FromRgb(0x090D16),
        UiColor.FromRgb(0x172033),
        UiColor.FromRgb(0x56B8FF),
        UiColor.FromRgb(0x06101A),
        UiColor.FromRgb(0xF2F8FF),
        UiColor.FromRgb(0xA9BAD0),
        UiColor.FromRgb(0x36506D),
        UiColor.FromRgb(0xFF6174));
}

public sealed class GameHud : Component<GameTheme>
{
    public State<GameFrame> CurrentFrame { get; } = new(default);

    public State<bool> MenuOpen { get; } = new(true);

    public void Update(GameFrame frame) => CurrentFrame.Value = frame;

    protected override void Compose(Ui<GameTheme> ui)
    {
        GameFrame frame = CurrentFrame.Value;
        ui.DIV(screen =>
        {
            screen.Semantics(UiRole.Group, "游戏 HUD");
            screen.Color.Text();
            screen.Padding.All(18);
            screen.Width.Full();
            screen.DIV(hud =>
            {
                hud.Semantics(UiRole.Status, "游戏状态");
                hud.BackgroundColor.Surface();
                hud.Padding.All(12);
                hud.Gap.Px(4);
                hud.Width.Px(300);
                hud.Border.Width(1).Radius(10).DefaultColor();
                hud.H1("ZEngine Slice", heading =>
                    heading.Typography.Size(20).Weight(700));
                hud.TEXT($"生命 {frame.Gameplay.PlayerHealth:0.0}");
                hud.TEXT($"实体 {frame.Gameplay.EntityCount:N0}");
                hud.TEXT($"帧 {frame.PresentedFrame} · Pass {frame.RenderPassCount}");
                hud.BUTTON(MenuOpen.Value ? "关闭菜单" : "打开菜单", button =>
                {
                    button.Semantics(UiRole.Button, "切换菜单");
                    button.BackgroundColor.Primary();
                    button.Color.PrimaryText();
                    button.Padding.XY(10, 6);
                    button.Border.Radius(6);
                    button.OnClick(_ => MenuOpen.Value = !MenuOpen.Value);
                });
                if (MenuOpen.Value)
                {
                    hud.DIV(menu =>
                    {
                        menu.Semantics(UiRole.Group, "主菜单");
                        menu.BackgroundColor.Page();
                        menu.Padding.All(8);
                        menu.Gap.Px(3);
                        menu.Border.Radius(6);
                        menu.TEXT("继续游戏");
                        menu.TEXT("设置");
                        menu.TEXT("退出到桌面");
                    });
                }
            });
        });
    }
}
