using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.Services.Product.Domain.Entities;
using YiPix.Services.Product.Infrastructure.Data;

namespace YiPix.Services.Product.Application;

public record ProductDto(Guid Id, string Name, string? Description, string Slug, string? IconUrl, string? BannerUrl, string? Features, List<PricingPlanDto> Plans);
public record PricingPlanDto(Guid Id, string Name, string? Description, decimal Price, string Currency, string BillingCycle, string? PayPalPlanId, string? FeatureList);
public record CreateProductRequest(string Name, string? Description, string Slug, string? IconUrl, string? BannerUrl, string? Features);
public record CreatePlanRequest(Guid ProductId, string Name, string? Description, decimal Price, string Currency, string BillingCycle, string? PayPalPlanId, string? FeatureList, int SortOrder);

public interface IProductAppService
{
    Task<ProductDto?> GetProductAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<ProductDto>> GetAllProductsAsync(CancellationToken ct = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<PricingPlanDto> AddPricingPlanAsync(CreatePlanRequest request, CancellationToken ct = default);
}

public class ProductAppService : IProductAppService
{
    private readonly IProductRepository _repository;

    public ProductAppService(IProductRepository repository) => _repository = repository;

    public async Task<ProductDto?> GetProductAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(id, ct);
        return product == null ? null : MapToDto(product);
    }

    public async Task<ProductDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default)
    {
        var product = await _repository.GetBySlugAsync(slug, ct);
        return product == null ? null : MapToDto(product);
    }

    public async Task<List<ProductDto>> GetAllProductsAsync(CancellationToken ct = default)
    {
        var products = await _repository.GetAllAsync(ct);
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetBySlugAsync(request.Slug, ct);
        if (existing != null) throw new ConflictException($"Product with slug '{request.Slug}' already exists.");

        var product = new Domain.Entities.Product
        {
            Name = request.Name,
            Description = request.Description,
            Slug = request.Slug,
            IconUrl = request.IconUrl,
            BannerUrl = request.BannerUrl,
            Features = request.Features
        };

        await _repository.CreateAsync(product, ct);
        return MapToDto(product);
    }

    public async Task<PricingPlanDto> AddPricingPlanAsync(CreatePlanRequest request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, ct)
            ?? throw new NotFoundException("Product", request.ProductId);

        var plan = new PricingPlan
        {
            ProductId = request.ProductId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Currency = request.Currency,
            BillingCycle = request.BillingCycle,
            PayPalPlanId = request.PayPalPlanId,
            FeatureList = request.FeatureList,
            SortOrder = request.SortOrder
        };

        product.PricingPlans.Add(plan);
        await _repository.UpdateAsync(product, ct);
        return MapPlanToDto(plan);
    }

    private static ProductDto MapToDto(Domain.Entities.Product p)
        => new(p.Id, p.Name, p.Description, p.Slug, p.IconUrl, p.BannerUrl, p.Features,
            p.PricingPlans.Select(MapPlanToDto).ToList());

    private static PricingPlanDto MapPlanToDto(PricingPlan p)
        => new(p.Id, p.Name, p.Description, p.Price, p.Currency, p.BillingCycle, p.PayPalPlanId, p.FeatureList);
}
