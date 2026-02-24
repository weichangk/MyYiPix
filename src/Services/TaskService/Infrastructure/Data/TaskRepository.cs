using Microsoft.EntityFrameworkCore;
using YiPix.Services.TaskProcessing.Domain.Entities;

namespace YiPix.Services.TaskProcessing.Infrastructure.Data;

public interface ITaskRepository
{
    Task<ProcessingTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ProcessingTask>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<List<ProcessingTask>> GetPendingTasksAsync(int count = 10, CancellationToken ct = default);
    Task<ProcessingTask> CreateAsync(ProcessingTask task, CancellationToken ct = default);
    Task UpdateAsync(ProcessingTask task, CancellationToken ct = default);
}

public class TaskRepository : ITaskRepository
{
    private readonly TaskDbContext _context;

    public TaskRepository(TaskDbContext context) => _context = context;

    public async Task<ProcessingTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Tasks.FindAsync([id], ct);

    public async Task<List<ProcessingTask>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default)
        => await _context.Tasks
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<List<ProcessingTask>> GetPendingTasksAsync(int count = 10, CancellationToken ct = default)
        => await _context.Tasks
            .Where(t => t.Status == "Pending")
            .OrderBy(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<ProcessingTask> CreateAsync(ProcessingTask task, CancellationToken ct = default)
    {
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(ct);
        return task;
    }

    public async Task UpdateAsync(ProcessingTask task, CancellationToken ct = default)
    {
        task.UpdatedAt = DateTime.UtcNow;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync(ct);
    }
}
