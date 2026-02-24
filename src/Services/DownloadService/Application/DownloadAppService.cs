using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.Services.Download.Domain.Entities;
using YiPix.Services.Download.Infrastructure.Data;

namespace YiPix.Services.Download.Application;

public record ReleaseDto(Guid Id, string Version, string Platform, long FileSize, string? ReleaseNotes, bool IsLatest, DateTime ReleasedAt);
public record DownloadLinkResponse(string Url, DateTime ExpiresAt);
public record CreateReleaseRequest(string Version, string Platform, string DownloadUrl, string? FileHash, long FileSize, string? ReleaseNotes, string? MinSubscriptionPlan);

public interface IDownloadAppService
{
    Task<ReleaseDto?> GetLatestReleaseAsync(string platform, CancellationToken ct = default);
    Task<List<ReleaseDto>> GetAllReleasesAsync(CancellationToken ct = default);
    Task<DownloadLinkResponse> GetDownloadLinkAsync(string version, string platform, Guid? userId, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<ReleaseDto> CreateReleaseAsync(CreateReleaseRequest request, CancellationToken ct = default);
    Task<long> GetDownloadCountAsync(CancellationToken ct = default);
}

public class DownloadAppService : IDownloadAppService
{
    private readonly IDownloadRepository _repository;
    private readonly IEventBus _eventBus;

    public DownloadAppService(IDownloadRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
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

        // TODO: Generate CDN signed URL
        var signedUrl = $"{release.DownloadUrl}?token={Guid.NewGuid()}&expires={DateTime.UtcNow.AddHours(1).Ticks}";
        return new DownloadLinkResponse(signedUrl, DateTime.UtcNow.AddHours(1));
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
            MinSubscriptionPlan = request.MinSubscriptionPlan,
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
