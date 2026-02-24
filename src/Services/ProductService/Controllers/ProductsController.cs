using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.Services.Product.Application;

namespace YiPix.Services.Product.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductAppService _service;

    public ProductsController(IProductAppService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetAll(CancellationToken ct)
    {
        var result = await _service.GetAllProductsAsync(ct);
        return Ok(ApiResponse<List<ProductDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetProductAsync(id, ct);
        if (result == null) return NotFound(ApiResponse.Fail("Product not found."));
        return Ok(ApiResponse<ProductDto>.Ok(result));
    }

    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetBySlug(string slug, CancellationToken ct)
    {
        var result = await _service.GetProductBySlugAsync(slug, ct);
        if (result == null) return NotFound(ApiResponse.Fail("Product not found."));
        return Ok(ApiResponse<ProductDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create(
        [FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await _service.CreateProductAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<ProductDto>.Ok(result));
    }

    [HttpPost("plans")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<PricingPlanDto>>> AddPlan(
        [FromBody] CreatePlanRequest request, CancellationToken ct)
    {
        var result = await _service.AddPricingPlanAsync(request, ct);
        return Ok(ApiResponse<PricingPlanDto>.Ok(result));
    }
}
