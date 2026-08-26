namespace ZEngine.Ecs;

public readonly record struct Entity(int Index, uint Generation)
{
    public static Entity Null { get; } = new(-1, 0);

    public bool IsNull => Index < 0 || Generation == 0;

    public override string ToString() =>
        IsNull ? "Entity:null" : $"Entity:{Index}@{Generation}";
}
