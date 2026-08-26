using ZEngine.UI;
using ZEngine.UI.Basic;

namespace UiLab.Shared;

public sealed class AppTheme : SysTheme
{
    public override ThemeColors Colors { get; } = new(
        UiColor.FromRgb(0x0D1118),
        UiColor.FromRgb(0x18202C),
        UiColor.FromRgb(0x6C8EFF),
        UiColor.FromRgb(0xFFFFFF),
        UiColor.FromRgb(0xF7F9FC),
        UiColor.FromRgb(0xAEB9C9),
        UiColor.FromRgb(0x354154),
        UiColor.FromRgb(0xF06A6A));
}

public sealed class LoginCard : Component<AppTheme>
{
    public State<string> UserName { get; } = new(string.Empty);

    public State<int> LoginCount { get; } = new(0);

    protected override void Compose(Ui<AppTheme> ui)
    {
        ui.DIV(page =>
        {
            page.Semantics(UiRole.Group, "登录面板");
            page.BackgroundColor.Page();
            page.Color.Text();
            page.Display.Flex();
            page.Flex.Column().Align(AlignItems.Center);
            page.Padding.All(32);
            page.Gap.Px(16);
            page.Width.Full();

            page.DIV(card =>
            {
                card.Semantics(UiRole.Group, "登录卡片");
                card.BackgroundColor.Surface();
                card.Color.Text();
                card.Padding.All(24);
                card.Gap.Px(12);
                card.Width.Px(420);
                card.Border.Width(1).Radius(16).DefaultColor();
                card.Shadow.Medium();
                card.H1("欢迎回来", heading =>
                {
                    heading.Semantics(UiRole.Heading, "欢迎回来");
                    heading.Typography.Size(28).Weight(700);
                });
                card.TEXT("使用系统字体显示中文，所有结构与样式都来自强类型 C# DSL。");
                card.INPUT(input =>
                {
                    input.Semantics(UiRole.TextBox, "用户名");
                    input.Value(UserName.Value);
                    input.Placeholder("请输入用户名");
                    input.Padding.XY(12, 10);
                    input.Border.Width(1).Radius(8).DefaultColor();
                    input.OnInput(HandleInput);
                });
                card.BUTTON("登录", button =>
                {
                    button.Semantics(UiRole.Button, "登录");
                    button.BackgroundColor.Primary();
                    button.Color.PrimaryText();
                    button.Padding.XY(16, 10);
                    button.Border.Radius(8);
                    button.OnClick(HandleLogin);
                });
                card.TEXT($"{UserName.Value} 已点击登录 {LoginCount.Value} 次");
                card.DIV(
                    new[] { "键盘", "手柄", "Agent" },
                    static (capabilities, items) =>
                    {
                        capabilities.Flex.Row().Justify(JustifyContent.SpaceBetween);
                        capabilities.Gap.Px(8);
                        foreach (string item in items)
                        {
                            capabilities.SPAN(
                                badge =>
                                {
                                    badge.BackgroundColor.Primary();
                                    badge.Color.PrimaryText();
                                    badge.Padding.XY(8, 4);
                                    badge.Border.Radius(6);
                                    badge.TEXT(item);
                                },
                                key: item);
                        }
                    });
            });
        });
    }

    private void HandleInput(UiInputEvent input) => UserName.Value = input.Value;

    private void HandleLogin(UiClickEvent _) => LoginCount.Value++;
}
