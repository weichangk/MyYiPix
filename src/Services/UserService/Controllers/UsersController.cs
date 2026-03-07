using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.User.Application;

namespace YiPix.Services.User.Controllers;

/// <summary>
/// 用户控制器 - 管理用户资料、健康状态和行为日志
/// 所有接口需要 JWT 认证
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserProfileService _userProfileService;

    public UsersController(IUserProfileService userProfileService)
    {
        _userProfileService = userProfileService;
    }

    /// <summary>获取用户资料</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile(Guid id)
    {
        var profile = await _userProfileService.GetProfileAsync(id);
        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }

    /// <summary>更新用户资料（部分更新，仅修改传入的字段）</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile(Guid id, [FromBody] UpdateProfileRequest request)
    {
        var profile = await _userProfileService.UpdateProfileAsync(id, request);
        return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Profile updated successfully."));
    }

    /// <summary>停用用户账户（软删除）</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeactivateUser(Guid id)
    {
        await _userProfileService.DeactivateUserAsync(id);
        return Ok(ApiResponse.Ok("User deactivated successfully."));
    }

    /// <summary>分页查询用户操作日志</summary>
    [HttpGet("{id:guid}/activities")]
    public async Task<ActionResult<ApiResponse<List<Domain.Entities.UserActivity>>>> GetActivities(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var activities = await _userProfileService.GetUserActivitiesAsync(id, page, pageSize);
        return Ok(ApiResponse<List<Domain.Entities.UserActivity>>.Ok(activities));
    }
}
