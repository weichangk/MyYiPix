namespace YiPix.Services.FileStorage.Infrastructure.Storage;

/// <summary>
/// 文件存储服务接口 - 抽象存储后端，支持 Local/MinIO/S3 实现切换
/// </summary>
public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string? category = null, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken ct = default);
}
