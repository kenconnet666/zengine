using ZEngine.Plugins.Runtime;
using Xunit;

namespace ZEngine.Plugins.Tests;

public sealed class PluginGraphTests
{
    [Fact]
    public void ResolvesDagVersionsAndReverseRequiredClosure()
    {
        PluginManifest a = Manifest("A", "1.2.0");
        PluginManifest b = Manifest(
            "B",
            "1.0.0",
            new PluginDependency("A", "[1.0.0,2.0.0)"));
        PluginManifest c = Manifest(
            "C",
            "1.0.0",
            new PluginDependency("B", "[1.0.0,2.0.0)"));
        PluginManifest d = Manifest("D", "1.0.0");
        PluginManifest optional = Manifest(
            "E",
            "1.0.0",
            new PluginDependency(
                "A",
                "[1.0.0,2.0.0)",
                PluginDependencyKind.Optional));

        PluginGraphPlan plan = PluginGraph.Resolve([c, d, b, optional, a]);

        Assert.True(
            IndexOf(plan, "A") < IndexOf(plan, "B")
            && IndexOf(plan, "B") < IndexOf(plan, "C"));
        Assert.Equal(["C", "B", "A"], plan.ReverseRequiredClosure("A"));
        Assert.DoesNotContain("D", plan.ReverseRequiredClosure("A"));
        Assert.DoesNotContain("E", plan.ReverseRequiredClosure("A"));
    }

    [Fact]
    public void RejectsMissingVersionAndCycleWithDiagnosticPath()
    {
        PluginManifest incompatible = Manifest(
            "B",
            "1.0.0",
            new PluginDependency("A", "[2.0.0,3.0.0)"));
        InvalidOperationException version = Assert.Throws<InvalidOperationException>(() =>
            PluginGraph.Resolve([Manifest("A", "1.2.0"), incompatible]));
        Assert.Contains("resolved 1.2.0", version.Message);

        PluginManifest a = Manifest("A", "1.0.0", new PluginDependency("C", "*"));
        PluginManifest b = Manifest("B", "1.0.0", new PluginDependency("A", "*"));
        PluginManifest c = Manifest("C", "1.0.0", new PluginDependency("B", "*"));
        InvalidOperationException cycle = Assert.Throws<InvalidOperationException>(() =>
            PluginGraph.Resolve([a, b, c]));
        Assert.Contains("cycle", cycle.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A", cycle.Message);
        Assert.Contains("B", cycle.Message);
        Assert.Contains("C", cycle.Message);
    }

    private static int IndexOf(PluginGraphPlan plan, string id) =>
        plan.ActivationOrder
            .Select((manifest, index) => (manifest, index))
            .Single(item => item.manifest.Id == id)
            .index;

    private static PluginManifest Manifest(
        string id,
        string version,
        params PluginDependency[] dependencies) =>
        new(
            id,
            version,
            "Plugin.dll",
            "Plugin.Entry",
            [],
            dependencies,
            []);
}
