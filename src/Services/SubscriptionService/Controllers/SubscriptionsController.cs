using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.BuildingBlocks.Contracts.Subscription;
using YiPix.Services.Subscription.Application;

namespace YiPix.Services.Subscription.Controllers;

/// <summary>
/// 订阅控制器 - 查询、创建、取消订阅
/// 所有接口需要 JWT 认证
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionAppService _service;

    public SubscriptionsController(ISubscriptionAppService service) => _service = service;

    /// <summary>获取用户所有订阅记录</summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<List<SubscriptionDto>>>> GetUserSubscriptions(
        Guid userId, CancellationToken ct)
    {
        var result = await _service.GetUserSubscriptionsAsync(userId, ct);
        return Ok(ApiResponse<List<SubscriptionDto>>.Ok(result));
    }

    /// <summary>获取用户当前活跃订阅</summary>
    [HttpGet("user/{userId:guid}/active")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetActive(
        Guid userId, CancellationToken ct)
    {
        var result = await _service.GetActiveSubscriptionAsync(userId, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }

    /// <summary>检查用户订阅状态（是否活跃、当前计划、到期时间）</summary>
    [HttpGet("user/{userId:guid}/status")]
    public async Task<ActionResult<ApiResponse<SubscriptionStatusResponse>>> CheckStatus(
        Guid userId, CancellationToken ct)
    {
        var result = await _service.CheckStatusAsync(userId, ct);
        return Ok(ApiResponse<SubscriptionStatusResponse>.Ok(result));
    }

    /// <summary>创建新订阅（每个用户同时只能有一个活跃订阅）</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> Create(
        [FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        var result = await _service.CreateSubscriptionAsync(request, ct);
        return CreatedAtAction(nameof(GetActive), new { userId = request.UserId },
            ApiResponse<SubscriptionDto>.Ok(result));
    }

    /// <summary>取消订阅</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse>> Cancel(
        Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        await _service.CancelSubscriptionAsync(id, reason, ct);
        return Ok(ApiResponse.Ok("Subscription cancelled."));
    }
}
