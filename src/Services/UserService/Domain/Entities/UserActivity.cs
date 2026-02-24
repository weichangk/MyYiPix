using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.User.Domain.Entities;

public class UserActivity : BaseEntity
{
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
}
