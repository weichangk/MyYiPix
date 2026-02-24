using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.TaskProcessing.Application;

namespace YiPix.Services.TaskProcessing.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskAppService _service;

    public TasksController(ITaskAppService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TaskDto>>> Create(
        [FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var result = await _service.CreateTaskAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<TaskDto>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TaskDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetTaskAsync(id, ct);
        if (result == null) return NotFound(ApiResponse.Fail("Task not found."));
        return Ok(ApiResponse<TaskDto>.Ok(result));
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<List<TaskDto>>>> GetByUser(
        Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetUserTasksAsync(userId, page, pageSize, ct);
        return Ok(ApiResponse<List<TaskDto>>.Ok(result));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<TaskDto>>> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _service.CancelTaskAsync(id, ct);
        return Ok(ApiResponse<TaskDto>.Ok(result));
    }
}
