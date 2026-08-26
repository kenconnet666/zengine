namespace ZEngine.UI;

internal interface IStateSource
{
    event Action? Changed;
}

public sealed class State<T> : IStateSource
{
    private T _value;

    public State(T value)
    {
        _value = value;
    }

    public event Action? Changed;

    public T Value
    {
        get
        {
            UiCompositionContext.Track(this);
            return _value;
        }
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }

            _value = value;
            Version++;
            Changed?.Invoke();
        }
    }

    public ulong Version { get; private set; }
}

internal static class UiCompositionContext
{
    [ThreadStatic]
    private static Action<IStateSource>? _track;

    public static IDisposable Begin(Action<IStateSource> track)
    {
        Action<IStateSource>? previous = _track;
        _track = track;
        return new Scope(previous);
    }

    public static void Track(IStateSource state) => _track?.Invoke(state);

    private sealed class Scope(Action<IStateSource>? previous) : IDisposable
    {
        public void Dispose() => _track = previous;
    }
}
