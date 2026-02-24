using Microsoft.EntityFrameworkCore;
using YiPix.Services.Product.Domain.Entities;

namespace YiPix.Services.Product.Infrastructure.Data;

public interface IProductRepository
{
    Task<Domain.Entities.Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Domain.Entities.Product?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<Domain.Entities.Product>> GetAllAsync(CancellationToken ct = default);
    Task<Domain.Entities.Product> CreateAsync(Domain.Entities.Product product, CancellationToken ct = default);
    Task UpdateAsync(Domain.Entities.Product product, CancellationToken ct = default);
    Task<PricingPlan?> GetPlanByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<PricingPlan>> GetPlansByProductIdAsync(Guid productId, CancellationToken ct = default);
}

public class ProductRepository : IProductRepository
{
    private readonly ProductDbContext _context;

    public ProductRepository(ProductDbContext context) => _context = context;

    public async Task<Domain.Entities.Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Products.Include(p => p.PricingPlans).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Domain.Entities.Product?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _context.Products.Include(p => p.PricingPlans).FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task<List<Domain.Entities.Product>> GetAllAsync(CancellationToken ct = default)
        => await _context.Products.Include(p => p.PricingPlans).Where(p => p.IsActive).ToListAsync(ct);

    public async Task<Domain.Entities.Product> CreateAsync(Domain.Entities.Product product, CancellationToken ct = default)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync(ct);
        return product;
    }

    public async Task UpdateAsync(Domain.Entities.Product product, CancellationToken ct = default)
    {
        product.UpdatedAt = DateTime.UtcNow;
        _context.Products.Update(product);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PricingPlan?> GetPlanByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PricingPlans.FindAsync([id], ct);

    public async Task<List<PricingPlan>> GetPlansByProductIdAsync(Guid productId, CancellationToken ct = default)
        => await _context.PricingPlans.Where(p => p.ProductId == productId && p.IsActive)
            .OrderBy(p => p.SortOrder).ToListAsync(ct);
}
