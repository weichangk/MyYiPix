using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.Services.User.Domain.Entities;
using YiPix.Services.User.Infrastructure.Data;

namespace YiPix.Services.User.Application;

// DTOs
public record UserProfileDto(
    Guid Id,
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? Country,
    string? Language,
    string? Bio,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record UpdateProfileRequest(
    string? DisplayName,
    string? AvatarUrl,
    string? Country,
    string? Language,
    string? Bio);

// Interface
public interface IUserProfileService
{
    Task<UserProfileDto> GetProfileAsync(Guid userId);
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task DeactivateUserAsync(Guid userId);
    Task<List<UserActivity>> GetUserActivitiesAsync(Guid userId, int page = 1, int pageSize = 20);
}

// Implementation
public class UserProfileService : IUserProfileService
{
    private readonly IUserRepository _repository;

    public UserProfileService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId)
    {
        var profile = await _repository.GetByUserIdAsync(userId)
            ?? throw new YiPixException("User profile not found.", 404);

        return MapToDto(profile);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var profile = await _repository.GetByUserIdAsync(userId)
            ?? throw new YiPixException("User profile not found.", 404);

        if (request.DisplayName is not null) profile.DisplayName = request.DisplayName;
        if (request.AvatarUrl is not null) profile.AvatarUrl = request.AvatarUrl;
        if (request.Country is not null) profile.Country = request.Country;
        if (request.Language is not null) profile.Language = request.Language;
        if (request.Bio is not null) profile.Bio = request.Bio;

        var updated = await _repository.UpdateAsync(profile);
        return MapToDto(updated);
    }

    public async Task DeactivateUserAsync(Guid userId)
    {
        var profile = await _repository.GetByUserIdAsync(userId)
            ?? throw new YiPixException("User profile not found.", 404);

        profile.IsActive = false;
        await _repository.UpdateAsync(profile);
    }

    public async Task<List<UserActivity>> GetUserActivitiesAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        return await _repository.GetActivitiesByUserIdAsync(userId, page, pageSize);
    }

    private static UserProfileDto MapToDto(UserProfile profile) => new(
        profile.Id,
        profile.UserId,
        profile.Email,
        profile.DisplayName,
        profile.AvatarUrl,
        profile.Country,
        profile.Language,
        profile.Bio,
        profile.IsActive,
        profile.CreatedAt,
        profile.UpdatedAt);
}
