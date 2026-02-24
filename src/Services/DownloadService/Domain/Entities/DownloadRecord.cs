using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Download.Domain.Entities;

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
