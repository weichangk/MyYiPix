using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Product.Domain.Entities;

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
