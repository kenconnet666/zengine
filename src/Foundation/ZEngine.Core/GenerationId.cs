namespace ZEngine.Core;

public readonly record struct GenerationId(ulong Value)
{
    public static GenerationId Initial => new(1);

    public GenerationId Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString();
}
