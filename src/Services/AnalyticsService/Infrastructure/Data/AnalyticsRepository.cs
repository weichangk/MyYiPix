using Microsoft.EntityFrameworkCore;
using YiPix.Services.Analytics.Domain.Entities;

namespace YiPix.Services.Analytics.Infrastructure.Data;

public interface IAnalyticsRepository
{
    Task TrackEventAsync(AnalyticsEvent evt, CancellationToken ct = default);
    Task<List<AnalyticsEvent>> GetEventsAsync(string? eventType, DateTime? from, DateTime? to, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<long> GetEventCountAsync(string eventType, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task UpsertDailyStatsAsync(DateOnly date, string metricName, long value, string? dimension = null, CancellationToken ct = default);
    Task<List<DailyStats>> GetDailyStatsAsync(string metricName, DateOnly from, DateOnly to, CancellationToken ct = default);
}

public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly AnalyticsDbContext _context;

    public AnalyticsRepository(AnalyticsDbContext context) => _context = context;

    public async Task TrackEventAsync(AnalyticsEvent evt, CancellationToken ct = default)
    {
        _context.Events.Add(evt);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<AnalyticsEvent>> GetEventsAsync(string? eventType, DateTime? from, DateTime? to, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = _context.Events.AsQueryable();
        if (!string.IsNullOrEmpty(eventType)) query = query.Where(e => e.EventType == eventType);
        if (from.HasValue) query = query.Where(e => e.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(e => e.CreatedAt <= to.Value);
        return await query.OrderByDescending(e => e.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    public async Task<long> GetEventCountAsync(string eventType, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _context.Events.Where(e => e.EventType == eventType);
        if (from.HasValue) query = query.Where(e => e.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(e => e.CreatedAt <= to.Value);
        return await query.LongCountAsync(ct);
    }

    public async Task UpsertDailyStatsAsync(DateOnly date, string metricName, long value, string? dimension = null, CancellationToken ct = default)
    {
        var existing = await _context.DailyStats
            .FirstOrDefaultAsync(d => d.Date == date && d.MetricName == metricName && d.Dimension == dimension, ct);

        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.DailyStats.Add(new DailyStats
            {
                Date = date,
                MetricName = metricName,
                Value = value,
                Dimension = dimension
            });
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<DailyStats>> GetDailyStatsAsync(string metricName, DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _context.DailyStats
            .Where(d => d.MetricName == metricName && d.Date >= from && d.Date <= to)
            .OrderBy(d => d.Date)
            .ToListAsync(ct);
}
