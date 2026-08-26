namespace ZEngine.UI.Sdk;

public enum UiBackend
{
    Web,
    Native
}

public readonly record struct UiRenderKey(
    UiBackend Backend,
    string ComponentKind);

public sealed record UiRendererRegistration(
    string PackageId,
    UiRenderKey Key,
    Delegate Factory);

public sealed class UiEnhancementRegistry
{
    private readonly Dictionary<UiRenderKey, UiRendererRegistration> _renderers = [];
    private readonly object _gate = new();

    public IReadOnlyCollection<UiRendererRegistration> Renderers
    {
        get
        {
            lock (_gate)
            {
                return _renderers.Values.ToArray();
            }
        }
    }

    public IDisposable RegisterPrimaryRenderer(
        string packageId,
        UiRenderKey key,
        Delegate factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.ComponentKind);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            if (_renderers.TryGetValue(
                    key,
                    out UiRendererRegistration? existing))
            {
                throw new UiPackageConflictException(
                    "ZUI4102",
                    $"Primary renderer conflict for {key.Backend}/{key.ComponentKind}: "
                    + $"'{existing.PackageId}' and '{packageId}'.");
            }

            UiRendererRegistration registration = new(packageId, key, factory);
            _renderers.Add(key, registration);
            return new Registration(this, key, registration);
        }
    }

    private void Remove(
        UiRenderKey key,
        UiRendererRegistration registration)
    {
        lock (_gate)
        {
            if (_renderers.TryGetValue(key, out UiRendererRegistration? current)
                && ReferenceEquals(current, registration))
            {
                _renderers.Remove(key);
            }
        }
    }

    private sealed class Registration(
        UiEnhancementRegistry owner,
        UiRenderKey key,
        UiRendererRegistration registration) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Remove(key, registration);
            }
        }
    }
}

public sealed class UiPackageConflictException(
    string diagnosticId,
    string message) : InvalidOperationException(message)
{
    public string DiagnosticId { get; } = diagnosticId;
}
