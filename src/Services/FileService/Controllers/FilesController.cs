using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.FileStorage.Application;

namespace YiPix.Services.FileStorage.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileAppService _service;

    public FilesController(IFileAppService service) => _service = service;

    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<UploadResult>>> Upload(
        IFormFile file, [FromQuery] string? category, [FromQuery] bool isPublic = false, CancellationToken ct = default)
    {
        if (file.Length == 0) return BadRequest(ApiResponse.Fail("File is empty."));

        using var stream = file.OpenReadStream();
        var result = await _service.UploadAsync(stream, file.FileName, file.ContentType, file.Length, null, category, isPublic, ct);
        return Ok(ApiResponse<UploadResult>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await _service.DownloadAsync(id, ct);
        if (result == null) return NotFound();
        return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<List<FileDto>>>> GetUserFiles(
        Guid userId, [FromQuery] string? category, CancellationToken ct)
    {
        var result = await _service.GetUserFilesAsync(userId, category, ct);
        return Ok(ApiResponse<List<FileDto>>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("File deleted."));
    }
}
