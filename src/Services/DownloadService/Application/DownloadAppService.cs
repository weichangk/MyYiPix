using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.Services.Download.Domain.Entities;
using YiPix.Services.Download.Infrastructure.Cdn;
using YiPix.Services.Download.Infrastructure.Data;

namespace YiPix.Services.Download.Application;

// ========== DTOs ==========
public record ReleaseDto(Guid Id, string Version, string Platform, long FileSize, string? ReleaseNotes, bool IsLatest, DateTime ReleasedAt);
/// <summary>CDN 签名下载链接响应</summary>
public record DownloadLinkResponse(string Url, DateTime ExpiresAt);
public record CreateReleaseRequest(string Version, string Platform, string DownloadUrl, string? FileHash, long FileSize, string? ReleaseNotes);

/// <summary>
/// 下载服务接口 - 版本管理、CDN 签名链接生成、下载统计
/// </summary>
public interface IDownloadAppService
{
    Task<ReleaseDto?> GetLatestReleaseAsync(string platform, CancellationToken ct = default);
    Task<List<ReleaseDto>> GetAllReleasesAsync(CancellationToken ct = default);
    Task<DownloadLinkResponse> GetDownloadLinkAsync(string version, string platform, Guid? userId, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<ReleaseDto> CreateReleaseAsync(CreateReleaseRequest request, CancellationToken ct = default);
    Task<long> GetDownloadCountAsync(CancellationToken ct = default);
}

/// <summary>
/// 下载服务实现：通过 ICdnSignService 生成带鉴权签名的下载 URL
/// </summary>
public class DownloadAppService : IDownloadAppService
{
    private readonly IDownloadRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICdnSignService _cdnSignService;

    public DownloadAppService(
        IDownloadRepository repository,
        IEventBus eventBus,
        ICdnSignService cdnSignService)
    {
        _repository = repository;
        _eventBus = eventBus;
        _cdnSignService = cdnSignService;
    }

    public async Task<ReleaseDto?> GetLatestReleaseAsync(string platform, CancellationToken ct = default)
    {
        var release = await _repository.GetLatestReleaseAsync(platform, ct);
        return release == null ? null : MapToDto(release);
    }

    public async Task<List<ReleaseDto>> GetAllReleasesAsync(CancellationToken ct = default)
    {
        var releases = await _repository.GetAllReleasesAsync(ct);
        return releases.Select(MapToDto).ToList();
    }

    public async Task<DownloadLinkResponse> GetDownloadLinkAsync(string version, string platform, Guid? userId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var release = await _repository.GetReleaseAsync(version, platform, ct)
            ?? throw new NotFoundException("Release", Guid.Empty);

        // Record download
        await _repository.AddDownloadRecordAsync(new DownloadRecord
        {
            UserId = userId,
            ReleaseId = release.Id,
            Version = version,
            Platform = platform,
            IpAddress = ipAddress,
            UserAgent = userAgent
        }, ct);

        if (userId.HasValue)
        {
            await _eventBus.PublishAsync(
                new DownloadStartedEvent(userId.Value, version, platform), ct);
        }

        // CDN 签名
        var signed = _cdnSignService.GenerateSignedUrl(release.DownloadUrl);
        return new DownloadLinkResponse(signed.Url, signed.ExpiresAt);
    }

    public async Task<ReleaseDto> CreateReleaseAsync(CreateReleaseRequest request, CancellationToken ct = default)
    {
        var release = new SoftwareRelease
        {
            Version = request.Version,
            Platform = request.Platform,
            DownloadUrl = request.DownloadUrl,
            FileHash = request.FileHash,
            FileSize = request.FileSize,
            ReleaseNotes = request.ReleaseNotes,
            IsLatest = true
        };

        // Unset previous latest
        var previousLatest = await _repository.GetLatestReleaseAsync(request.Platform, ct);
        if (previousLatest != null)
        {
            previousLatest.IsLatest = false;
            await _repository.UpdateReleaseAsync(previousLatest, ct);
        }

        await _repository.CreateReleaseAsync(release, ct);
        return MapToDto(release);
    }

    public async Task<long> GetDownloadCountAsync(CancellationToken ct = default)
        => await _repository.GetDownloadCountAsync(ct: ct);

    private static ReleaseDto MapToDto(SoftwareRelease r)
        => new(r.Id, r.Version, r.Platform, r.FileSize, r.ReleaseNotes, r.IsLatest, r.ReleasedAt);
}
