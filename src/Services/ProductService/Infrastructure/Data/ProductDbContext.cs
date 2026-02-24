using Microsoft.EntityFrameworkCore;
using YiPix.Services.Product.Domain.Entities;

namespace YiPix.Services.Product.Infrastructure.Data;

public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    public DbSet<Domain.Entities.Product> Products => Set<Domain.Entities.Product>();
    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("product");

        modelBuilder.Entity<Domain.Entities.Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<PricingPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PayPalPlanId);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.Product)
                  .WithMany(p => p.PricingPlans)
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
