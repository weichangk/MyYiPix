using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Analytics.Domain.Entities;

public class AnalyticsEvent : BaseEntity
{
    public Guid? UserId { get; set; }
    
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? EventCategory { get; set; }
    
    public string? EventData { get; set; } // JSON payload
    
    [MaxLength(100)]
    public string? Source { get; set; } // Website, Desktop, Admin
    
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    [MaxLength(50)]
    public string? Country { get; set; }
}
