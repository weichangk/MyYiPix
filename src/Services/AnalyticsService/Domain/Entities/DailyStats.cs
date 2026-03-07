using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Analytics.Domain.Entities;

/// <summary>
/// 每日统计指标实体 - 按 Date+MetricName+Dimension 唯一
/// 由 AnalyticsWorker 异步聚合生成
/// </summary>
public class DailyStats : BaseEntity
{
    public DateOnly Date { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public long Value { get; set; }
    public string? Dimension { get; set; }
}
