using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.Analytics.Application;

namespace YiPix.Services.Analytics.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsAppService _service;

    public AnalyticsController(IAnalyticsAppService service) => _service = service;

    [HttpPost("track")]
    public async Task<ActionResult<ApiResponse>> Track(
        [FromBody] TrackEventRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        await _service.TrackEventAsync(request, ip, ua, ct);
        return Ok(ApiResponse.Ok("Event tracked."));
    }

    [HttpGet("events/count")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<EventCountResponse>>> GetEventCount(
        [FromQuery] string eventType, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await _service.GetEventCountAsync(eventType, from, to, ct);
        return Ok(ApiResponse<EventCountResponse>.Ok(result));
    }

    [HttpGet("daily")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<List<DailyStatsDto>>>> GetDailyStats(
        [FromQuery] string metricName, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        var result = await _service.GetDailyStatsAsync(metricName, from, to, ct);
        return Ok(ApiResponse<List<DailyStatsDto>>.Ok(result));
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<DashboardSummary>>> GetDashboard(CancellationToken ct)
    {
        var result = await _service.GetDashboardSummaryAsync(ct);
        return Ok(ApiResponse<DashboardSummary>.Ok(result));
    }
}
