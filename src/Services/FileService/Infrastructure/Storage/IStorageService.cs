namespace YiPix.Services.FileStorage.Infrastructure.Storage;

public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string? category = null, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken ct = default);
}
