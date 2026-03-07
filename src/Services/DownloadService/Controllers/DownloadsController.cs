using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.Download.Application;

namespace YiPix.Services.Download.Controllers;

/// <summary>
/// 下载控制器 - 管理软件版本发布和 CDN 签名下载链接生成
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DownloadsController : ControllerBase
{
    private readonly IDownloadAppService _service;

    public DownloadsController(IDownloadAppService service) => _service = service;

    /// <summary>获取指定平台的最新版本信息</summary>
    [HttpGet("latest/{platform}")]
    public async Task<ActionResult<ApiResponse<ReleaseDto>>> GetLatest(string platform, CancellationToken ct)
    {
        var result = await _service.GetLatestReleaseAsync(platform, ct);
        if (result == null) return NotFound(ApiResponse.Fail("No release found."));
        return Ok(ApiResponse<ReleaseDto>.Ok(result));
    }

    /// <summary>获取所有已发布版本列表</summary>
    [HttpGet("releases")]
    public async Task<ActionResult<ApiResponse<List<ReleaseDto>>>> GetAll(CancellationToken ct)
    {
        var result = await _service.GetAllReleasesAsync(ct);
        return Ok(ApiResponse<List<ReleaseDto>>.Ok(result));
    }

    /// <summary>生成带 CDN 签名的下载链接（记录下载行为）</summary>
    [HttpGet("link/{version}/{platform}")]
    public async Task<ActionResult<ApiResponse<DownloadLinkResponse>>> GetLink(
        string version, string platform, CancellationToken ct)
    {
        Guid? userId = null; // Extract from JWT claims if authenticated
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var result = await _service.GetDownloadLinkAsync(version, platform, userId, ip, ua, ct);
        return Ok(ApiResponse<DownloadLinkResponse>.Ok(result));
    }

    /// <summary>发布新版本（仅管理员，自动将旧版本标记为非最新）</summary>
    [HttpPost("releases")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<ReleaseDto>>> CreateRelease(
        [FromBody] CreateReleaseRequest request, CancellationToken ct)
    {
        var result = await _service.CreateReleaseAsync(request, ct);
        return CreatedAtAction(nameof(GetLatest), new { platform = request.Platform },
            ApiResponse<ReleaseDto>.Ok(result));
    }

    [HttpGet("stats/count")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<long>>> GetCount(CancellationToken ct)
    {
        var count = await _service.GetDownloadCountAsync(ct);
        return Ok(ApiResponse<long>.Ok(count));
    }
}
