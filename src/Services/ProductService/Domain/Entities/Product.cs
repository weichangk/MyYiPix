using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Product.Domain.Entities;

/// <summary>
/// 产品实体（聚合根）- 包含产品基本信息和关联的定价方案列表
/// Features 字段存储 JSON 格式的功能列表
/// </summary>
public class Product : AggregateRoot
{
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(50)]
    public string Slug { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    [MaxLength(500)]
    public string? IconUrl { get; set; }
    
    [MaxLength(500)]
    public string? BannerUrl { get; set; }
    
    public string? Features { get; set; } // JSON array of features
    
    public ICollection<PricingPlan> PricingPlans { get; set; } = [];
}
