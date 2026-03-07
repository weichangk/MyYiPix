using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Payment.Domain.Entities;

/// <summary>
/// Webhook 日志实体 - 记录每次 PayPal 回调，用于幂等性检查和问题追溯
/// </summary>
public class WebhookLog : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool Processed { get; set; }
    public string? ProcessingError { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
