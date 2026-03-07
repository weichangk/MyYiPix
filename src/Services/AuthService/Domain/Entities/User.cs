using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.Auth.Domain.Entities;

/// <summary>
/// 认证用户实体（聚合根）- 存储认证所需的账户信息
/// 与 UserService 中的 UserProfile 通过 UserId 关联
/// </summary>
public class User : AggregateRoot
{
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [MaxLength(20)]
    public string Role { get; set; } = "User";

    public bool IsActive { get; set; } = true;

    public bool EmailConfirmed { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
