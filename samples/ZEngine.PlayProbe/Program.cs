Console.WriteLine("ZEngine play process initialized.");
if (args.Contains("--crash", StringComparer.Ordinal))
{
    Console.Error.WriteLine("Intentional isolated game crash.");
    return 23;
}

Console.WriteLine("ZEngine play process completed.");
return 0;

namespace ZEngine.PlayProbe
{
    public sealed class PlayProbeMarker;
}
