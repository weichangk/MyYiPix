using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.User.Application;

namespace YiPix.Services.User.Controllers;

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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile(Guid id)
    {
        var profile = await _userProfileService.GetProfileAsync(id);
        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile(Guid id, [FromBody] UpdateProfileRequest request)
    {
        var profile = await _userProfileService.UpdateProfileAsync(id, request);
        return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Profile updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeactivateUser(Guid id)
    {
        await _userProfileService.DeactivateUserAsync(id);
        return Ok(ApiResponse.Ok("User deactivated successfully."));
    }

    [HttpGet("{id:guid}/activities")]
    public async Task<ActionResult<ApiResponse<List<Domain.Entities.UserActivity>>>> GetActivities(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var activities = await _userProfileService.GetUserActivitiesAsync(id, page, pageSize);
        return Ok(ApiResponse<List<Domain.Entities.UserActivity>>.Ok(activities));
    }
}
