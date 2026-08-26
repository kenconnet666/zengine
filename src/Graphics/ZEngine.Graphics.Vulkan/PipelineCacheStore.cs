namespace ZEngine.Graphics.Vulkan;

public static class PipelineCacheStore
{
    public static async Task<byte[]> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? await File.ReadAllBytesAsync(fullPath, cancellationToken)
            : [];
    }

    public static async Task SaveAtomicAsync(
        string path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath =
            fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(
                temporaryPath,
                data,
                cancellationToken);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
