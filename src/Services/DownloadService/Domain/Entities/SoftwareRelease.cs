using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Download.Domain.Entities;

/// <summary>
/// 软件版本发布实体（聚合根）
/// DownloadUrl 存储 CDN 资源路径（如 /releases/v1.0.0/setup.exe），签名服务会自动拼接域名和签名参数
/// </summary>
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
    
    public DateTime ReleasedAt { get; set; } = DateTime.UtcNow;
}
