using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Download.Domain.Entities;

public class SoftwareRelease : AggregateRoot
{
    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Platform { get; set; } = string.Empty; // Windows, macOS, Linux
    
    [MaxLength(500)]
    public string DownloadUrl { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? FileHash { get; set; }
    
    public long FileSize { get; set; }
    
    public string? ReleaseNotes { get; set; }
    
    public bool IsLatest { get; set; }
    public bool IsActive { get; set; } = true;
    
    [MaxLength(20)]
    public string? MinSubscriptionPlan { get; set; } // null = free, Monthly, Yearly, Lifetime
    
    public DateTime ReleasedAt { get; set; } = DateTime.UtcNow;
}
