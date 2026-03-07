using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Product.Domain.Entities;

/// <summary>
/// 定价方案实体 - 支持 Monthly/Yearly/OneTime 计费周期
/// PayPalPlanId 用于关联 PayPal 订阅计划
/// </summary>
public class PricingPlan : BaseEntity
{
    public Guid ProductId { get; set; }
    
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty; // Monthly, Yearly, Lifetime
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public decimal Price { get; set; }
    
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";
    
    [MaxLength(50)]
    public string BillingCycle { get; set; } = "Monthly"; // Monthly, Yearly, OneTime
    
    [MaxLength(200)]
    public string? PayPalPlanId { get; set; }
    
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    
    public string? FeatureList { get; set; } // JSON array
    
    public Product Product { get; set; } = null!;
}
