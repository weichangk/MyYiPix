using Microsoft.EntityFrameworkCore;
using YiPix.Services.Download.Domain.Entities;

namespace YiPix.Services.Download.Infrastructure.Data;

public class DownloadDbContext : DbContext
{
    public DownloadDbContext(DbContextOptions<DownloadDbContext> options) : base(options) { }

    public DbSet<SoftwareRelease> Releases => Set<SoftwareRelease>();
    public DbSet<DownloadRecord> DownloadRecords => Set<DownloadRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("download");

        modelBuilder.Entity<SoftwareRelease>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Version, e.Platform }).IsUnique();
            entity.Property(e => e.Version).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DownloadUrl).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<DownloadRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ReleaseId);
            entity.HasOne(e => e.Release)
                  .WithMany()
                  .HasForeignKey(e => e.ReleaseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
