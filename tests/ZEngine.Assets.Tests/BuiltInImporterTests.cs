using System.Buffers.Binary;
using ZEngine.AssetImporters;
using ZEngine.Jobs;
using ZEngine.Plugins;
using Xunit;

namespace ZEngine.Assets.Tests;

public sealed class BuiltInImporterTests
{
    [Fact]
    public async Task ImporterPluginOwnsRegistrationsAndEachAssetReloadsIndependently()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string root = Path.Combine(
            Path.GetTempPath(),
            "zengine-importers-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string shader = Path.Combine(root, "surface.spv");
            string texture = Path.Combine(root, "albedo.png");
            string scene = Path.Combine(root, "level.scene.json");
            await File.WriteAllBytesAsync(shader, ValidSpirv(1), cancellation);
            await File.WriteAllBytesAsync(texture, ValidPngHeader(32, 16), cancellation);
            await File.WriteAllTextAsync(scene, "{\"name\":\"level-a\"}", cancellation);

            AssetImporterRegistry registry = new();
            using JobScheduler jobs = new(1);
            await using PluginScope scope = new(
                "zengine.asset-importers",
                ZEngine.Core.GenerationId.Initial,
                jobs.CreateGroup(ZEngine.Core.GenerationId.Initial),
                new Dictionary<Type, object> { [typeof(AssetImporterRegistry)] = registry });
            new AssetImporterPlugin().Configure(scope);
            AssetPipeline pipeline = new(registry, new(Path.Combine(root, "cache")));
            AssetMonitor<ShaderAsset> shaderMonitor = new(
                pipeline,
                AssetId<ShaderAsset>.FromName("shaders/surface"),
                shader,
                "zengine.shader.spirv");
            AssetMonitor<TextureAsset> textureMonitor = new(
                pipeline,
                AssetId<TextureAsset>.FromName("textures/albedo"),
                texture,
                "zengine.texture.png");
            AssetMonitor<SceneAsset> sceneMonitor = new(
                pipeline,
                AssetId<SceneAsset>.FromName("scenes/level"),
                scene,
                "zengine.scene.json");

            Assert.True(await shaderMonitor.RefreshAsync(cancellation));
            Assert.True(await textureMonitor.RefreshAsync(cancellation));
            Assert.True(await sceneMonitor.RefreshAsync(cancellation));
            ContentHash shaderHash = shaderMonitor.Handle.Current.Hash;
            ContentHash textureHash = textureMonitor.Handle.Current.Hash;
            ContentHash sceneHash = sceneMonitor.Handle.Current.Hash;

            await File.WriteAllBytesAsync(texture, ValidPngHeader(64, 16), cancellation);
            Assert.True(await textureMonitor.RefreshAsync(cancellation));
            Assert.NotEqual(textureHash, textureMonitor.Handle.Current.Hash);
            Assert.False(await shaderMonitor.RefreshAsync(cancellation));
            Assert.False(await sceneMonitor.RefreshAsync(cancellation));
            Assert.Equal(shaderHash, shaderMonitor.Handle.Current.Hash);
            Assert.Equal(sceneHash, sceneMonitor.Handle.Current.Hash);

            await scope.DisposeAsync();
            Assert.Throws<KeyNotFoundException>(() =>
                registry.Get("zengine.texture.png"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FailedImportKeepsLastCompleteGeneration()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string root = Path.Combine(
            Path.GetTempPath(),
            "zengine-import-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string texture = Path.Combine(root, "texture.png");
            await File.WriteAllBytesAsync(texture, ValidPngHeader(8, 8), cancellation);
            AssetImporterRegistry registry = new();
            using IDisposable registration = registry.Register(new PngTextureImporter());
            AssetMonitor<TextureAsset> monitor = new(
                new(registry, new(Path.Combine(root, "cache"))),
                AssetId<TextureAsset>.FromName("textures/failure-safe"),
                texture,
                "zengine.texture.png");
            Assert.True(await monitor.RefreshAsync(cancellation));
            AssetArtifact previous = monitor.Handle.Current;
            await File.WriteAllTextAsync(texture, "broken", cancellation);

            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await monitor.RefreshAsync(cancellation));
            Assert.Same(previous, monitor.Handle.Current);
            Assert.Equal(1ul, monitor.Handle.Generation);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] ValidSpirv(uint generator)
    {
        byte[] bytes = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x07230203);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x00010600);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), generator);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 1);
        return bytes;
    }

    private static byte[] ValidPngHeader(uint width, uint height)
    {
        byte[] bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }
}
