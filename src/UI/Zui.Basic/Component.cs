namespace ZEngine.UI.Basic;

public abstract class Component<TTheme> : UiComponent<TTheme>
    where TTheme : SysTheme
{
    protected sealed override void Compose(UiComposer<TTheme> composer) =>
        Compose(new Ui<TTheme>(composer));

    protected abstract void Compose(Ui<TTheme> ui);
}
