using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.User.Domain.Entities;

public class UserProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Bio { get; set; }
    public bool IsActive { get; set; } = true;
}
