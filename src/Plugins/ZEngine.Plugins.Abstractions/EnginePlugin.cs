namespace ZEngine.Plugins;

public abstract class EnginePlugin
{
    public abstract void Configure(PluginScope scope);

    public virtual void SaveHotState(HotStateWriter state)
    {
    }

    public virtual void RestoreHotState(HotStateReader state)
    {
    }

    public virtual void OnActivated()
    {
    }

    public virtual void OnDeactivated()
    {
    }
}
