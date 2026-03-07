using Microsoft.EntityFrameworkCore;
using YiPix.Services.Analytics.Domain.Entities;

namespace YiPix.Services.Analytics.Infrastructure.Data;

/// <summary>
/// Analytics 数据库上下文 - 使用 "analytics" schema 隔离
/// 包含 Events 和 DailyStats 两张表
/// </summary>
public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options) { }

    public DbSet<AnalyticsEvent> Events => Set<AnalyticsEvent>();
    public DbSet<DailyStats> DailyStats => Set<DailyStats>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("analytics");

        modelBuilder.Entity<AnalyticsEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<DailyStats>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Date, e.MetricName, e.Dimension }).IsUnique();
        });
    }
}
