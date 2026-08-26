using ZEngine.ChainPlugin.Contracts;
using ZEngine.Plugins;

namespace ZEngine.ChainPlugin.Runtime;

public sealed class PluginA : EnginePlugin
{
    public override void Configure(PluginScope scope) =>
        scope.Export<IChainA>(new ServiceA());
}

public sealed class PluginB : EnginePlugin
{
    public override void Configure(PluginScope scope) =>
        scope.Export<IChainB>(new ServiceB(scope.Require<IChainA>()));
}

public sealed class PluginC : EnginePlugin
{
    public override void Configure(PluginScope scope) =>
        scope.Export<IChainC>(new ServiceC(scope.Require<IChainB>()));
}

public sealed class PluginD : EnginePlugin
{
    public override void Configure(PluginScope scope) =>
        scope.Export<IChainD>(new ServiceD());
}

internal abstract class DisposableService : IDisposable
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

internal sealed class ServiceA : DisposableService, IChainA;

internal sealed class ServiceB(IChainA provider)
    : DisposableService, IChainB
{
    public IChainA Provider { get; } = provider;
}

internal sealed class ServiceC(IChainB provider)
    : DisposableService, IChainC
{
    public IChainB Provider { get; } = provider;
}

internal sealed class ServiceD : DisposableService, IChainD;
