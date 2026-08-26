namespace ZEngine.CounterPlugin.Contracts;

public interface ICounterService
{
    int Value { get; set; }

    bool IsDisposed { get; }

    Task WorkStarted { get; }

    void StartBlockingWork();

    void ReleaseWork();
}
