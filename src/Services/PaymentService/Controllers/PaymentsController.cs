using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using YiPix.BuildingBlocks.Common.Models;
using YiPix.BuildingBlocks.Contracts.Payment;
using YiPix.BuildingBlocks.PayPal;
using YiPix.Services.Payment.Application;
using PayPalOptions = YiPix.BuildingBlocks.PayPal.PayPalOptions;

namespace YiPix.Services.Payment.Controllers;

/// <summary>
/// 支付控制器 - 管理支付订单和处理 PayPal Webhook
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentAppService _service;
    private readonly IPayPalClient _paypalClient;
    private readonly PayPalOptions _paypalOptions;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentAppService service,
        IPayPalClient paypalClient,
        IOptions<PayPalOptions> paypalOptions,
        ILogger<PaymentsController> logger)
    {
        _service = service;
        _paypalClient = paypalClient;
        _paypalOptions = paypalOptions.Value;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPayment(Guid id, CancellationToken ct)
    {
        var result = await _service.GetPaymentAsync(id, ct);
        if (result == null) return NotFound(ApiResponse.Fail("Payment not found.", code: 404));
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
        return CreatedAtAction(nameof(GetPayment), new { id = result.Id }, ApiResponse<PaymentDto>.Ok(result, code: 201));
    }

    [HttpPost("capture")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CapturePayment(
        [FromBody] CapturePaymentRequest request, CancellationToken ct)
    {
        var result = await _service.CapturePaymentAsync(request, ct);
        return Ok(ApiResponse<PaymentDto>.Ok(result));
    }

    /// <summary>
    /// PayPal Webhook 回调入口（无需认证）
    /// 1. 验证 PayPal Webhook 签名（安全性）
    /// 2. 解析 JSON 提取 event_type 和 resource.id
    /// 3. 委托给 PaymentAppService 做幂等分发处理
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        // 读取原始请求体
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        _logger.LogInformation("Received PayPal webhook callback");

        // ① 验证 PayPal Webhook 签名
        if (!string.IsNullOrEmpty(_paypalOptions.WebhookId))
        {
            var headers = new Dictionary<string, string>
            {
                ["PAYPAL-AUTH-ALGO"] = Request.Headers["PAYPAL-AUTH-ALGO"].ToString(),
                ["PAYPAL-CERT-URL"] = Request.Headers["PAYPAL-CERT-URL"].ToString(),
                ["PAYPAL-TRANSMISSION-ID"] = Request.Headers["PAYPAL-TRANSMISSION-ID"].ToString(),
                ["PAYPAL-TRANSMISSION-SIG"] = Request.Headers["PAYPAL-TRANSMISSION-SIG"].ToString(),
                ["PAYPAL-TRANSMISSION-TIME"] = Request.Headers["PAYPAL-TRANSMISSION-TIME"].ToString()
            };

            var isValid = await _paypalClient.VerifyWebhookSignatureAsync(
                _paypalOptions.WebhookId, headers, payload, ct);

            if (!isValid)
            {
                _logger.LogWarning("PayPal webhook signature verification failed");
                return Unauthorized("Invalid webhook signature");
            }
        }
        else
        {
            _logger.LogWarning("WebhookId not configured, skipping signature verification (NOT safe for production)");
        }

        // ② 解析 JSON 提取 event_type 和 resource.id
        try
        {
            using var jsonDoc = JsonDocument.Parse(payload);
            var root = jsonDoc.RootElement;

            var eventType = root.GetProperty("event_type").GetString() ?? "UNKNOWN";
            var resourceId = string.Empty;

            if (root.TryGetProperty("resource", out var resource))
            {
                if (resource.TryGetProperty("id", out var idProp))
                    resourceId = idProp.GetString() ?? "";
            }

            _logger.LogInformation("PayPal webhook event: {EventType}, resource: {ResourceId}",
                eventType, resourceId);

            // ③ 委托处理
            await _service.ProcessWebhookAsync(eventType, resourceId, payload, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse PayPal webhook payload");
            return BadRequest("Invalid JSON payload");
        }

        return Ok();
    }
}
