namespace ZEngine.Ecs;

public sealed class EntityCommandBuffer
{
    private readonly List<IStructuralCommand> _commands = [];

    public int Count => _commands.Count;

    public void Add<T>(Entity entity, in T component)
        where T : unmanaged =>
        _commands.Add(new AddCommand<T>(entity, component));

    public void Remove<T>(Entity entity)
        where T : unmanaged =>
        _commands.Add(new RemoveCommand<T>(entity));

    public void Destroy(Entity entity) =>
        _commands.Add(new DestroyCommand(entity));

    public int Flush(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        int applied = 0;
        try
        {
            for (; applied < _commands.Count; applied++)
            {
                _commands[applied].Apply(world);
            }

            return applied;
        }
        finally
        {
            _commands.Clear();
        }
    }

    private interface IStructuralCommand
    {
        void Apply(World world);
    }

    private sealed class AddCommand<T>(Entity entity, T component)
        : IStructuralCommand
        where T : unmanaged
    {
        public void Apply(World world) => world.Add(entity, component);
    }

    private sealed class RemoveCommand<T>(Entity entity)
        : IStructuralCommand
        where T : unmanaged
    {
        public void Apply(World world) => world.Remove<T>(entity);
    }

    private sealed class DestroyCommand(Entity entity) : IStructuralCommand
    {
        public void Apply(World world) => world.Destroy(entity);
    }
}
