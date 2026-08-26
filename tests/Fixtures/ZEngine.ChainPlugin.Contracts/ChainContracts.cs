namespace ZEngine.ChainPlugin.Contracts;

public interface IChainA
{
    Guid InstanceId { get; }

    bool IsDisposed { get; }
}

public interface IChainB
{
    Guid InstanceId { get; }

    IChainA Provider { get; }

    bool IsDisposed { get; }
}

public interface IChainC
{
    Guid InstanceId { get; }

    IChainB Provider { get; }

    bool IsDisposed { get; }
}

public interface IChainD
{
    Guid InstanceId { get; }

    bool IsDisposed { get; }
}
