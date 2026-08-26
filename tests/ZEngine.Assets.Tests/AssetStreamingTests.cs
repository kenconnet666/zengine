using System.Text;
using Xunit;

namespace ZEngine.Assets.Tests;

public sealed class AssetStreamingTests
{
    [Fact]
    public async Task SharesResidentRevisionAndRetiresOnlyAfterLastLease()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string root = Path.Combine(
            Path.GetTempPath(),
            "zengine-stream-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] data = Encoding.UTF8.GetBytes("streamed-content");
            string dataPath = Path.Combine(root, "asset.bin");
            await File.WriteAllBytesAsync(dataPath, data, cancellation);
            AssetId<StreamedAsset> id = AssetId<StreamedAsset>.FromName("stream/test");
            AssetHandle<StreamedAsset> handle = new(id);
            handle.Swap(new(
                new(new string('A', 64)),
                dataPath,
                "application/octet-stream",
                data.Length,
                "test",
                "1.0.0",
                []));
            AssetStreamCache<StreamedAsset> cache = new();

            using AssetLease<StreamedAsset> first = await cache.AcquireAsync(
                handle,
                cancellation);
            using AssetLease<StreamedAsset> second = await cache.AcquireAsync(
                handle,
                cancellation);
            AssetResidency resident = Assert.Single(cache.Snapshot());
            Assert.Equal(2, resident.References);
            Assert.Equal(data, first.Data.ToArray());
            Assert.True(first.Data.Span.Overlaps(second.Data.Span));

            first.Dispose();
            Assert.Equal(1, Assert.Single(cache.Snapshot()).References);
            second.Dispose();
            Assert.Empty(cache.Snapshot());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StreamedAsset;
}
