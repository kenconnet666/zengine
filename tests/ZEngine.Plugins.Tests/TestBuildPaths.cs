namespace ZEngine.Plugins.Tests;

internal static class TestBuildPaths
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string Configuration = FindConfiguration();

    public static string FixtureOutput(string project) =>
        Path.Combine(
            RepositoryRoot,
            "tests",
            "Fixtures",
            project,
            "bin",
            Configuration,
            "net11.0");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ZEngine.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate ZEngine.slnx above {AppContext.BaseDirectory}.");
    }

    private static string FindConfiguration()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (current.Name is "Debug" or "Release")
            {
                return current.Name;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not infer the test build configuration from {AppContext.BaseDirectory}.");
    }
}
