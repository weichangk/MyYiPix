using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.User.Domain.Entities;

/// <summary>
/// 用户行为日志实体 - 记录用户操作（登录、下载、设置更改等）
/// </summary>
public class UserActivity : BaseEntity
{
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
}
