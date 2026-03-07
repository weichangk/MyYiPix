using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.Analytics.Application;

namespace YiPix.Services.Analytics.Controllers;

/// <summary>
/// 统计分析控制器 - 事件埋点、数据查询和 Dashboard 汇总
/// 埋点接口公开，查询接口仅管理员可用
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsAppService _service;

    public AnalyticsController(IAnalyticsAppService service) => _service = service;

    /// <summary>埋点事件上报（自动采集 IP 和 UserAgent）</summary>
    [HttpPost("track")]
    public async Task<ActionResult<ApiResponse>> Track(
        [FromBody] TrackEventRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        await _service.TrackEventAsync(request, ip, ua, ct);
        return Ok(ApiResponse.Ok("Event tracked."));
    }

    /// <summary>按事件类型统计数量（支持时间范围过滤）</summary>
    [HttpGet("events/count")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<EventCountResponse>>> GetEventCount(
        [FromQuery] string eventType, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await _service.GetEventCountAsync(eventType, from, to, ct);
        return Ok(ApiResponse<EventCountResponse>.Ok(result));
    }

    /// <summary>按日查询统计指标</summary>
    [HttpGet("daily")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<List<DailyStatsDto>>>> GetDailyStats(
        [FromQuery] string metricName, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        var result = await _service.GetDailyStatsAsync(metricName, from, to, ct);
        return Ok(ApiResponse<List<DailyStatsDto>>.Ok(result));
    }

    /// <summary>获取 Dashboard 汇总数据（下载量、用户数、支付数、活跃订阅数）</summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<DashboardSummary>>> GetDashboard(CancellationToken ct)
    {
        var result = await _service.GetDashboardSummaryAsync(ct);
        return Ok(ApiResponse<DashboardSummary>.Ok(result));
    }
}
