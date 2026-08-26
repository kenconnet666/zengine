namespace ZEngine.UI;

public abstract class UiComponent<TTheme> : IDisposable
    where TTheme : SysTheme
{
    private readonly HashSet<IStateSource> _dependencies = [];
    private UiTree? _tree;
    private bool _dirty = true;
    private bool _disposed;
    private ulong _generation;

    public UiTree Render(TTheme theme)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_dirty && _tree is not null)
        {
            return _tree;
        }

        foreach (IStateSource state in _dependencies)
        {
            state.Changed -= MarkDirty;
        }

        _dependencies.Clear();
        UiComposer<TTheme> composer = new(theme);
        using (UiCompositionContext.Begin(Track))
        {
            Compose(composer);
        }

        UiTreeChange change = UiTreeDiffer.Compare(
            _tree?.Root,
            composer.Root);
        _tree = new(composer.Root, ++_generation, change);
        _dirty = false;
        return _tree;
    }

    public void Invalidate() => MarkDirty();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (IStateSource state in _dependencies)
        {
            state.Changed -= MarkDirty;
        }

        _dependencies.Clear();
        _disposed = true;
    }

    protected abstract void Compose(UiComposer<TTheme> composer);

    private void Track(IStateSource state)
    {
        if (_dependencies.Add(state))
        {
            state.Changed += MarkDirty;
        }
    }

    private void MarkDirty() => _dirty = true;
}
