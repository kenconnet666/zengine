using System.Text;
using Xunit;

namespace ZEngine.Assets.Tests;

public sealed class AssetPipelineTests
{
    [Fact]
    public async Task ContentAddressIncludesSourceImporterAndDependenciesAndSwapsAtomically()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string root = Path.Combine(
            Path.GetTempPath(),
            "zengine-assets-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "message.txt");
            string dependency = Path.Combine(root, "suffix.txt");
            await File.WriteAllTextAsync(source, "hello", cancellation);
            await File.WriteAllTextAsync(dependency, " world", cancellation);
            AssetImporterRegistry registry = new();
            using IDisposable registration = registry.Register(
                new TextImporter(dependency, "1.0.0"));
            AssetPipeline pipeline = new(
                registry,
                new(Path.Combine(root, "cache")));
            AssetMonitor<TextAsset> monitor = new(
                pipeline,
                AssetId<TextAsset>.FromName("tests/message"),
                source,
                "tests.text");

            Assert.True(await monitor.RefreshAsync(cancellation));
            AssetArtifact first = monitor.Handle.Current;
            Assert.Equal(1ul, monitor.Handle.Generation);
            Assert.Equal("HELLO WORLD", await File.ReadAllTextAsync(
                first.DataPath,
                cancellation));
            Assert.False(await monitor.RefreshAsync(cancellation));
            Assert.Equal(1ul, monitor.Handle.Generation);

            await File.WriteAllTextAsync(dependency, " codex", cancellation);
            Assert.True(await monitor.RefreshAsync(cancellation));
            AssetArtifact second = monitor.Handle.Current;
            Assert.NotEqual(first.Hash, second.Hash);
            Assert.Equal(2ul, monitor.Handle.Generation);
            Assert.Equal("HELLO CODEX", await File.ReadAllTextAsync(
                second.DataPath,
                cancellation));
            Assert.Empty(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories));

            registration.Dispose();
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await pipeline.BuildAsync(source, "tests.text", cancellation));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TypedAssetIdRejectsMalformedHash()
    {
        Assert.Throws<FormatException>(() => new ContentHash("not-a-hash"));
        AssetId<TextAsset> id = AssetId<TextAsset>.FromName("game/text");
        Assert.StartsWith("TextAsset:", id.ToString());
        Assert.Equal(id, AssetId<TextAsset>.FromName("game/text"));
        Assert.NotEqual(id, AssetId<TextAsset>.FromName("game/other"));
    }

    private sealed class TextAsset;

    private sealed class TextImporter(
        string dependency,
        string version) : IAssetImporter
    {
        public string Id => "tests.text";

        public string Version => version;

        public async ValueTask<AssetImportResult> ImportAsync(
            AssetImportContext context,
            CancellationToken cancellationToken)
        {
            string suffix = context.AddDependency(dependency);
            string sourceText = Encoding.UTF8.GetString(
                await context.ReadSourceAsync(cancellationToken));
            string suffixText = await File.ReadAllTextAsync(
                suffix,
                cancellationToken);
            return new(
                Encoding.UTF8.GetBytes((sourceText + suffixText).ToUpperInvariant()),
                "text/plain");
        }
    }
}
