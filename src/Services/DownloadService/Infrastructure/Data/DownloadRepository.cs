using Microsoft.EntityFrameworkCore;
using YiPix.Services.Download.Domain.Entities;

namespace YiPix.Services.Download.Infrastructure.Data;

public interface IDownloadRepository
{
    Task<SoftwareRelease?> GetLatestReleaseAsync(string platform, CancellationToken ct = default);
    Task<SoftwareRelease?> GetReleaseAsync(string version, string platform, CancellationToken ct = default);
    Task<List<SoftwareRelease>> GetAllReleasesAsync(CancellationToken ct = default);
    Task<SoftwareRelease> CreateReleaseAsync(SoftwareRelease release, CancellationToken ct = default);
    Task UpdateReleaseAsync(SoftwareRelease release, CancellationToken ct = default);
    Task AddDownloadRecordAsync(DownloadRecord record, CancellationToken ct = default);
    Task<long> GetDownloadCountAsync(Guid? releaseId = null, CancellationToken ct = default);
}

public class DownloadRepository : IDownloadRepository
{
    private readonly DownloadDbContext _context;

    public DownloadRepository(DownloadDbContext context) => _context = context;

    public async Task<SoftwareRelease?> GetLatestReleaseAsync(string platform, CancellationToken ct = default)
        => await _context.Releases
            .Where(r => r.Platform == platform && r.IsActive && r.IsLatest)
            .FirstOrDefaultAsync(ct);

    public async Task<SoftwareRelease?> GetReleaseAsync(string version, string platform, CancellationToken ct = default)
        => await _context.Releases
            .FirstOrDefaultAsync(r => r.Version == version && r.Platform == platform && r.IsActive, ct);

    public async Task<List<SoftwareRelease>> GetAllReleasesAsync(CancellationToken ct = default)
        => await _context.Releases.Where(r => r.IsActive).OrderByDescending(r => r.ReleasedAt).ToListAsync(ct);

    public async Task<SoftwareRelease> CreateReleaseAsync(SoftwareRelease release, CancellationToken ct = default)
    {
        _context.Releases.Add(release);
        await _context.SaveChangesAsync(ct);
        return release;
    }

    public async Task UpdateReleaseAsync(SoftwareRelease release, CancellationToken ct = default)
    {
        release.UpdatedAt = DateTime.UtcNow;
        _context.Releases.Update(release);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddDownloadRecordAsync(DownloadRecord record, CancellationToken ct = default)
    {
        _context.DownloadRecords.Add(record);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<long> GetDownloadCountAsync(Guid? releaseId = null, CancellationToken ct = default)
    {
        var query = _context.DownloadRecords.AsQueryable();
        if (releaseId.HasValue)
            query = query.Where(r => r.ReleaseId == releaseId.Value);
        return await query.LongCountAsync(ct);
    }
}
