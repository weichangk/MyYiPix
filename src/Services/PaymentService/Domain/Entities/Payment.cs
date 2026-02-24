using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Payment.Domain.Entities;

public class Payment : AggregateRoot
{
    public Guid UserId { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded
    
    public decimal Amount { get; set; }
    
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";
    
    [MaxLength(200)]
    public string? PayPalOrderId { get; set; }
    
    [MaxLength(200)]
    public string? PayPalSubscriptionId { get; set; }
    
    [MaxLength(50)]
    public string PaymentType { get; set; } = "Subscription"; // Subscription, OneTime
    
    [MaxLength(50)]
    public string? PlanId { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}
