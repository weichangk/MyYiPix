using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.BuildingBlocks.Contracts.Auth;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.BuildingBlocks.Security;
using YiPix.Services.Auth.Domain.Entities;
using YiPix.Services.Auth.Infrastructure.Data;
using BCrypt.Net;

namespace YiPix.Services.Auth.Application;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IAuthRepository _repository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly IEventBus _eventBus;

    public AuthService(
        IAuthRepository repository,
        IJwtTokenService jwtTokenService,
        JwtSettings jwtSettings,
        IEventBus eventBus)
    {
        _repository = repository;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtSettings;
        _eventBus = eventBus;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByEmailAsync(request.Email, ct);
        if (existing != null)
            throw new ConflictException("A user with this email already exists.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            Role = "User"
        };

        await _repository.CreateAsync(user, ct);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        await _repository.AddRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
        }, ct);

        await _eventBus.PublishAsync(new UserCreatedEvent(user.Id, user.Email, user.DisplayName), ct);

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        var user = await _repository.GetByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        if (!user.IsActive)
            throw new ForbiddenException("Account is deactivated.");

        user.LastLoginAt = DateTime.UtcNow;
        await _repository.UpdateAsync(user, ct);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        await _repository.AddRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            DeviceId = request.DeviceId,
            IpAddress = ipAddress
        }, ct);

        await _eventBus.PublishAsync(
            new UserLoggedInEvent(user.Id, ipAddress ?? "unknown", request.DeviceId ?? "unknown"), ct);

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        );
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var storedToken = await _repository.GetRefreshTokenAsync(request.RefreshToken, ct)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (!storedToken.IsActive)
            throw new UnauthorizedException("Refresh token is expired or revoked.");

        // Revoke old token
        await _repository.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        var user = storedToken.User;
        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();

        await _repository.AddRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            DeviceId = storedToken.DeviceId,
            IpAddress = storedToken.IpAddress
        }, ct);

        return new AuthResponse(
            accessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        );
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        await _repository.RevokeRefreshTokenAsync(refreshToken, ct);
    }
}
