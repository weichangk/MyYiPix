using Microsoft.EntityFrameworkCore;
using YiPix.Services.TaskProcessing.Domain.Entities;

namespace YiPix.Services.TaskProcessing.Infrastructure.Data;

/// <summary>
/// Task 数据库上下文 - 使用 "task" schema 隔离
/// 包含 Tasks 表，按 UserId 和 Status 建索引
/// </summary>
public class TaskDbContext : DbContext
{
    public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options) { }

    public DbSet<ProcessingTask> Tasks => Set<ProcessingTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("task");

        modelBuilder.Entity<ProcessingTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.TaskType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
        });
    }
}
