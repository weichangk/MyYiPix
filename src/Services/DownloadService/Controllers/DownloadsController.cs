using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.Download.Application;

namespace YiPix.Services.Download.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DownloadsController : ControllerBase
{
    private readonly IDownloadAppService _service;

    public DownloadsController(IDownloadAppService service) => _service = service;

    [HttpGet("latest/{platform}")]
    public async Task<ActionResult<ApiResponse<ReleaseDto>>> GetLatest(string platform, CancellationToken ct)
    {
        var result = await _service.GetLatestReleaseAsync(platform, ct);
        if (result == null) return NotFound(ApiResponse.Fail("No release found."));
        return Ok(ApiResponse<ReleaseDto>.Ok(result));
    }

    [HttpGet("releases")]
    public async Task<ActionResult<ApiResponse<List<ReleaseDto>>>> GetAll(CancellationToken ct)
    {
        var result = await _service.GetAllReleasesAsync(ct);
        return Ok(ApiResponse<List<ReleaseDto>>.Ok(result));
    }

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
