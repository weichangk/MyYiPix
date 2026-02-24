using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Payment.Domain.Entities;

public class WebhookLog : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool Processed { get; set; }
    public string? ProcessingError { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
