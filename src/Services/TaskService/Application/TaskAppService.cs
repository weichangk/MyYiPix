using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.Services.TaskProcessing.Domain.Entities;
using YiPix.Services.TaskProcessing.Infrastructure.Data;

namespace YiPix.Services.TaskProcessing.Application;

public record TaskDto(Guid Id, Guid UserId, string TaskType, string Status, int? Progress, string? InputFileUrl, string? OutputFileUrl, DateTime CreatedAt, DateTime? CompletedAt, string? ErrorMessage);
public record CreateTaskRequest(Guid UserId, string TaskType, string? InputFileUrl, string? Parameters);

public interface ITaskAppService
{
    Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto?> GetTaskAsync(Guid id, CancellationToken ct = default);
    Task<List<TaskDto>> GetUserTasksAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<TaskDto> UpdateTaskStatusAsync(Guid id, string status, int? progress = null, string? outputUrl = null, string? error = null, CancellationToken ct = default);
    Task<TaskDto> CancelTaskAsync(Guid id, CancellationToken ct = default);
}

public class TaskAppService : ITaskAppService
{
    private readonly ITaskRepository _repository;
    private readonly IEventBus _eventBus;

    public TaskAppService(ITaskRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        var task = new ProcessingTask
        {
            UserId = request.UserId,
            TaskType = request.TaskType,
            InputFileUrl = request.InputFileUrl,
            Parameters = request.Parameters,
            Status = "Pending"
        };

        await _repository.CreateAsync(task, ct);
        await _eventBus.PublishAsync(new TaskCreatedEvent(task.Id, task.UserId, task.TaskType), ct);
        return MapToDto(task);
    }

    public async Task<TaskDto?> GetTaskAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(id, ct);
        return task == null ? null : MapToDto(task);
    }

    public async Task<List<TaskDto>> GetUserTasksAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var tasks = await _repository.GetByUserIdAsync(userId, page, pageSize, ct);
        return tasks.Select(MapToDto).ToList();
    }

    public async Task<TaskDto> UpdateTaskStatusAsync(Guid id, string status, int? progress = null, string? outputUrl = null, string? error = null, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Task", id);

        task.Status = status;
        if (progress.HasValue) task.Progress = progress;
        if (outputUrl != null) task.OutputFileUrl = outputUrl;
        if (error != null) task.ErrorMessage = error;

        if (status == "Processing" && task.StartedAt == null) task.StartedAt = DateTime.UtcNow;
        if (status is "Completed" or "Failed") task.CompletedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(task, ct);

        if (status == "Completed")
            await _eventBus.PublishAsync(new TaskCompletedEvent(task.Id, task.UserId, task.TaskType), ct);
        else if (status == "Failed")
            await _eventBus.PublishAsync(new TaskFailedEvent(task.Id, task.UserId, error ?? "Unknown error"), ct);

        return MapToDto(task);
    }

    public async Task<TaskDto> CancelTaskAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Task", id);

        if (task.Status is "Completed" or "Failed")
            throw new YiPixException("Cannot cancel a completed or failed task.");

        task.Status = "Cancelled";
        task.CompletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(task, ct);
        return MapToDto(task);
    }

    private static TaskDto MapToDto(ProcessingTask t)
        => new(t.Id, t.UserId, t.TaskType, t.Status, t.Progress, t.InputFileUrl, t.OutputFileUrl, t.CreatedAt, t.CompletedAt, t.ErrorMessage);
}
