using YiPix.Services.Analytics.Domain.Entities;
using YiPix.Services.Analytics.Infrastructure.Data;

namespace YiPix.Services.Analytics.Application;

public record TrackEventRequest(string EventType, string? EventCategory, string? EventData, string? Source, Guid? UserId);
public record EventCountResponse(string EventType, long Count, DateTime? From, DateTime? To);
public record DailyStatsDto(DateOnly Date, string MetricName, long Value, string? Dimension);
public record DashboardSummary(long TotalDownloads, long TotalUsers, long TotalPayments, long ActiveSubscriptions);

public interface IAnalyticsAppService
{
    Task TrackEventAsync(TrackEventRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<EventCountResponse> GetEventCountAsync(string eventType, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<DailyStatsDto>> GetDailyStatsAsync(string metricName, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken ct = default);
}

public class AnalyticsAppService : IAnalyticsAppService
{
    private readonly IAnalyticsRepository _repository;

    public AnalyticsAppService(IAnalyticsRepository repository) => _repository = repository;

    public async Task TrackEventAsync(TrackEventRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        await _repository.TrackEventAsync(new AnalyticsEvent
        {
            UserId = request.UserId,
            EventType = request.EventType,
            EventCategory = request.EventCategory,
            EventData = request.EventData,
            Source = request.Source,
            IpAddress = ipAddress,
            UserAgent = userAgent
        }, ct);
    }

    public async Task<EventCountResponse> GetEventCountAsync(string eventType, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var count = await _repository.GetEventCountAsync(eventType, from, to, ct);
        return new EventCountResponse(eventType, count, from, to);
    }

    public async Task<List<DailyStatsDto>> GetDailyStatsAsync(string metricName, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var stats = await _repository.GetDailyStatsAsync(metricName, from, to, ct);
        return stats.Select(s => new DailyStatsDto(s.Date, s.MetricName, s.Value, s.Dimension)).ToList();
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var downloads = await _repository.GetEventCountAsync("download", null, null, ct);
        var users = await _repository.GetEventCountAsync("user_created", null, null, ct);
        var payments = await _repository.GetEventCountAsync("payment_completed", null, null, ct);
        var subscriptions = await _repository.GetEventCountAsync("subscription_activated", null, null, ct);
        return new DashboardSummary(downloads, users, payments, subscriptions);
    }
}
