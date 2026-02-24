using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.Services.FileStorage.Domain.Entities;
using YiPix.Services.FileStorage.Infrastructure.Data;
using YiPix.Services.FileStorage.Infrastructure.Storage;

namespace YiPix.Services.FileStorage.Application;

public record FileDto(Guid Id, string FileName, string ContentType, long FileSize, string? Category, bool IsPublic, DateTime CreatedAt);
public record UploadResult(Guid Id, string FileName, long FileSize, string Url);

public interface IFileAppService
{
    Task<UploadResult> UploadAsync(Stream stream, string fileName, string contentType, long fileSize, Guid? userId, string? category, bool isPublic, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(Guid id, CancellationToken ct = default);
    Task<List<FileDto>> GetUserFilesAsync(Guid userId, string? category, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class FileAppService : IFileAppService
{
    private readonly IFileRepository _repository;
    private readonly IStorageService _storage;

    public FileAppService(IFileRepository repository, IStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<UploadResult> UploadAsync(Stream stream, string fileName, string contentType, long fileSize, Guid? userId, string? category, bool isPublic, CancellationToken ct = default)
    {
        var storagePath = await _storage.UploadAsync(stream, fileName, contentType, category, ct);

        var file = new StoredFile
        {
            UserId = userId,
            FileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            StoragePath = storagePath,
            StorageProvider = "Local",
            Category = category,
            IsPublic = isPublic
        };

        await _repository.CreateAsync(file, ct);
        var url = await _storage.GetPresignedUrlAsync(storagePath, TimeSpan.FromHours(1), ct);
        return new UploadResult(file.Id, fileName, fileSize, url);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var file = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("File", id);

        var stream = await _storage.DownloadAsync(file.StoragePath, ct);
        if (stream == null) throw new NotFoundException("File", id);

        return (stream, file.ContentType, file.FileName);
    }

    public async Task<List<FileDto>> GetUserFilesAsync(Guid userId, string? category, CancellationToken ct = default)
    {
        var files = await _repository.GetByUserIdAsync(userId, category, ct);
        return files.Select(f => new FileDto(f.Id, f.FileName, f.ContentType, f.FileSize, f.Category, f.IsPublic, f.CreatedAt)).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var file = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("File", id);

        await _storage.DeleteAsync(file.StoragePath);
        await _repository.DeleteAsync(id, ct);
    }
}
