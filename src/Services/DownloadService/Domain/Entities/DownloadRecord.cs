using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Download.Domain.Entities;

/// <summary>
/// 下载记录实体 - 记录每次下载请求（用于统计和分析）
/// </summary>
public class DownloadRecord : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool Completed { get; set; }
    
    public SoftwareRelease Release { get; set; } = null!;
}
