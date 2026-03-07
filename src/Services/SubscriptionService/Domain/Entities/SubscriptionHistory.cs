using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Subscription.Domain.Entities;

/// <summary>
/// 订阅状态变更历史记录 - 记录每次状态转换的来源和原因
/// </summary>
public class SubscriptionHistory : BaseEntity
{
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    
    public Subscription Subscription { get; set; } = null!;
}
