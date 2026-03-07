using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.BuildingBlocks.Contracts.Auth;
using YiPix.Services.Auth.Application;

namespace YiPix.Services.Auth.Controllers;

/// <summary>
/// 认证控制器 - 处理用户注册、登录、Token 刷新和撤销
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>用户注册，返回 Access Token + Refresh Token</summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(
        [FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Registration successful."));
    }

    /// <summary>用户登录，验证密码后签发 Token</summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(request, ipAddress, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Login successful."));
    }

    /// <summary>使用 Refresh Token 换取新的 Access Token</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result));
    }

    /// <summary>撤销 Refresh Token（用于登出）</summary>
    [HttpPost("revoke")]
    public async Task<ActionResult<ApiResponse>> Revoke(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken, ct);
        return Ok(ApiResponse.Ok("Token revoked."));
    }
}
