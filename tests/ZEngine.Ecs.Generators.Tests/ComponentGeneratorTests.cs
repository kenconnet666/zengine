using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ZEngine.Ecs.Generators.Tests;

public sealed class ComponentGeneratorTests
{
    [Fact]
    public void GeneratesStableMetadataForUnmanagedPartialComponent()
    {
        const string source = """
            namespace ZEngine.Ecs
            {
                [System.AttributeUsage(System.AttributeTargets.Struct)]
                public sealed class ComponentAttribute : System.Attribute { }
            }

            namespace Game
            {
                [ZEngine.Ecs.Component]
                public partial struct Transform
                {
                    public float X;
                    public float Y;
                }
            }
            """;

        GeneratorDriverRunResult result = Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error));
        string generated = Assert.Single(result.GeneratedTrees)
            .GetText(TestContext.Current.CancellationToken)
            .ToString();
        Assert.Contains("public static class GeneratedSchema", generated);
        Assert.Contains("StableId = 0x", generated);
        Assert.Contains("Unsafe.SizeOf<global::Game.Transform>()", generated);
    }

    [Fact]
    public void RejectsManagedComponentField()
    {
        const string source = """
            namespace ZEngine.Ecs
            {
                [System.AttributeUsage(System.AttributeTargets.Struct)]
                public sealed class ComponentAttribute : System.Attribute { }
            }

            namespace Game
            {
                [ZEngine.Ecs.Component]
                public partial struct InvalidComponent
                {
                    public string Name;
                }
            }
            """;

        GeneratorDriverRunResult result = Run(source);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics.Where(item =>
            item.Id == "ZEC1002"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("managed type", diagnostic.GetMessage());
    }

    private static GeneratorDriverRunResult Run(string source)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.Preview);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        string[] trustedAssemblies = ((string)AppContext.GetData(
            "TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratorFixture",
            [syntaxTree],
            trustedAssemblies.Select(path => MetadataReference.CreateFromFile(path)),
            new(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ComponentGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out _);
        return driver.GetRunResult();
    }
}
