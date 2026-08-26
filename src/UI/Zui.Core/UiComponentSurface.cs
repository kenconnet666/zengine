namespace ZEngine.UI;

public sealed record UiSurfaceUpdate(
    int ComponentCount,
    IReadOnlyList<int> ChangedComponents,
    int ReusedComponents);

public sealed class UiComponentSurface<TTheme>
    where TTheme : SysTheme
{
    private readonly UiComponent<TTheme>[] _components;
    private readonly UiTree?[] _trees;

    public UiComponentSurface(IEnumerable<UiComponent<TTheme>> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components.ToArray();
        _trees = new UiTree[_components.Length];
    }

    public IReadOnlyList<UiTree?> Trees => _trees;

    public UiSurfaceUpdate Update(TTheme theme)
    {
        List<int> changed = [];
        for (int index = 0; index < _components.Length; index++)
        {
            UiTree next = _components[index].Render(theme);
            if (!ReferenceEquals(next, _trees[index]))
            {
                _trees[index] = next;
                changed.Add(index);
            }
        }

        return new(
            _components.Length,
            changed,
            _components.Length - changed.Count);
    }
}
