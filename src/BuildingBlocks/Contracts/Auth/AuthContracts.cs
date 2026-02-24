namespace YiPix.BuildingBlocks.Contracts.Auth;

public record RegisterRequest(string Email, string Password, string? DisplayName);
public record LoginRequest(string Email, string Password, string? DeviceId);
public record RefreshTokenRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
public record UserClaimsDto(Guid UserId, string Email, string Role, string? SubscriptionPlan);
