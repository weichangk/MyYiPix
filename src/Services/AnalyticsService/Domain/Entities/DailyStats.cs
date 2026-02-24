using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Analytics.Domain.Entities;

public class DailyStats : BaseEntity
{
    public DateOnly Date { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public long Value { get; set; }
    public string? Dimension { get; set; }
}
