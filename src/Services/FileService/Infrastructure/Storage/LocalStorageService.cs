namespace YiPix.Services.FileStorage.Infrastructure.Storage;

public class LocalStorageService : IStorageService
{
    private readonly string _basePath;

    public LocalStorageService(string basePath = "uploads")
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string? category = null, CancellationToken ct = default)
    {
        var relativePath = Path.Combine(category ?? "general", $"{Guid.NewGuid()}_{fileName}");
        var fullPath = Path.Combine(_basePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await stream.CopyToAsync(fileStream, ct);
        return relativePath;
    }

    public Task<Stream?> DownloadAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        if (!File.Exists(fullPath)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<string> GetPresignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken ct = default)
    {
        // Local storage doesn't support presigned URLs, return direct path
        return Task.FromResult($"/api/files/download/{Uri.EscapeDataString(storagePath)}");
    }
}
