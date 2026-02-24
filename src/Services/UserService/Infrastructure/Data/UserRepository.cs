using Microsoft.EntityFrameworkCore;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.User.Domain.Entities;

namespace YiPix.Services.User.Infrastructure.Data;

public interface IUserRepository
{
    Task<UserProfile?> GetByIdAsync(Guid id);
    Task<UserProfile?> GetByUserIdAsync(Guid userId);
    Task<UserProfile> CreateAsync(UserProfile profile);
    Task<UserProfile> UpdateAsync(UserProfile profile);
    Task DeleteAsync(Guid id);
    Task<PagedResult<UserProfile>> SearchUsersAsync(string? query, int page, int pageSize);
    Task<List<UserActivity>> GetActivitiesByUserIdAsync(Guid userId, int page, int pageSize);
    Task<UserActivity> AddActivityAsync(UserActivity activity);
}

public class UserRepository : IUserRepository
{
    private readonly UserDbContext _context;

    public UserRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfile?> GetByIdAsync(Guid id)
    {
        return await _context.UserProfiles.FindAsync(id);
    }

    public async Task<UserProfile?> GetByUserIdAsync(Guid userId)
    {
        return await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<UserProfile> CreateAsync(UserProfile profile)
    {
        _context.UserProfiles.Add(profile);
        await _context.SaveChangesAsync();
        return profile;
    }

    public async Task<UserProfile> UpdateAsync(UserProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        _context.UserProfiles.Update(profile);
        await _context.SaveChangesAsync();
        return profile;
    }

    public async Task DeleteAsync(Guid id)
    {
        var profile = await _context.UserProfiles.FindAsync(id);
        if (profile is not null)
        {
            _context.UserProfiles.Remove(profile);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<PagedResult<UserProfile>> SearchUsersAsync(string? query, int page, int pageSize)
    {
        var queryable = _context.UserProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            queryable = queryable.Where(u =>
                u.DisplayName.Contains(query) ||
                u.Email.Contains(query));
        }

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<UserProfile>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<List<UserActivity>> GetActivitiesByUserIdAsync(Guid userId, int page, int pageSize)
    {
        return await _context.UserActivities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<UserActivity> AddActivityAsync(UserActivity activity)
    {
        _context.UserActivities.Add(activity);
        await _context.SaveChangesAsync();
        return activity;
    }
}
