using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Subscription.Domain.Entities;

/// <summary>
/// 订阅实体（聚合根）- 支持 Free/Monthly/Yearly/Lifetime 计划
/// 状态流转：Active → Cancelled/Expired/PastDue/Suspended
/// </summary>
public class Subscription : AggregateRoot
{
    public Guid UserId { get; set; }
    
    [MaxLength(50)]
    public string Plan { get; set; } = "Free"; // Free, Monthly, Yearly, Lifetime
    
    [MaxLength(30)]
    public string Status { get; set; } = "Active"; // Active, Cancelled, Expired, PastDue, Suspended
    
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    
    [MaxLength(200)]
    public string? PayPalSubscriptionId { get; set; }
    
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    public bool IsActive => Status == "Active" && (EndDate == null || EndDate > DateTime.UtcNow);
}
