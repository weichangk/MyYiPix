using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.BuildingBlocks.Contracts.Subscription;
using YiPix.Services.Subscription.Application;

namespace YiPix.Services.Subscription.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionAppService _service;

    public SubscriptionsController(ISubscriptionAppService service) => _service = service;

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<List<SubscriptionDto>>>> GetUserSubscriptions(
        Guid userId, CancellationToken ct)
    {
        var result = await _service.GetUserSubscriptionsAsync(userId, ct);
        return Ok(ApiResponse<List<SubscriptionDto>>.Ok(result));
    }

    [HttpGet("user/{userId:guid}/active")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetActive(
        Guid userId, CancellationToken ct)
    {
        var result = await _service.GetActiveSubscriptionAsync(userId, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }

    [HttpGet("user/{userId:guid}/status")]
    public async Task<ActionResult<ApiResponse<SubscriptionStatusResponse>>> CheckStatus(
        Guid userId, CancellationToken ct)
    {
        var result = await _service.CheckStatusAsync(userId, ct);
        return Ok(ApiResponse<SubscriptionStatusResponse>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> Create(
        [FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        var result = await _service.CreateSubscriptionAsync(request, ct);
        return CreatedAtAction(nameof(GetActive), new { userId = request.UserId },
            ApiResponse<SubscriptionDto>.Ok(result));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse>> Cancel(
        Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        await _service.CancelSubscriptionAsync(id, reason, ct);
        return Ok(ApiResponse.Ok("Subscription cancelled."));
    }
}
