using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.BuildingBlocks.Contracts.Payment;
using YiPix.Services.Payment.Application;

namespace YiPix.Services.Payment.Controllers;

/// <summary>
/// 支付控制器 - 管理支付订单和处理 PayPal Webhook
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentAppService _service;

    public PaymentsController(IPaymentAppService service) => _service = service;

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPayment(Guid id, CancellationToken ct)
    {
        var result = await _service.GetPaymentAsync(id, ct);
        if (result == null) return NotFound(ApiResponse.Fail("Payment not found."));
        return Ok(ApiResponse<PaymentDto>.Ok(result));
    }

    [HttpGet("user/{userId:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<PaymentDto>>>> GetUserPayments(Guid userId, CancellationToken ct)
    {
        var result = await _service.GetUserPaymentsAsync(userId, ct);
        return Ok(ApiResponse<List<PaymentDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CreatePayment(
        [FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        var result = await _service.CreatePaymentAsync(request, ct);
        return CreatedAtAction(nameof(GetPayment), new { id = result.Id }, ApiResponse<PaymentDto>.Ok(result));
    }

    [HttpPost("capture")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CapturePayment(
        [FromBody] CapturePaymentRequest request, CancellationToken ct)
    {
        var result = await _service.CapturePaymentAsync(request, ct);
        return Ok(ApiResponse<PaymentDto>.Ok(result));
    }

    /// <summary>PayPal Webhook 回调入口（无需认证）</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        // TODO: 验证 PayPal Webhook 签名
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        // Simplified - in production parse the JSON properly
        await _service.ProcessWebhookAsync("PAYMENT.CAPTURE.COMPLETED", "resource-id", payload, ct);
        return Ok();
    }
}
