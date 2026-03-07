using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.User.Domain.Entities;

/// <summary>
/// 用户资料实体 - 存储用户的个人信息（昵称、头像、地区等）
/// 通过 UserId 与 AuthService 中的 User 实体关联
/// </summary>
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
