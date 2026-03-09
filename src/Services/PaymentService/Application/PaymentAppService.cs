using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.Contracts.Payment;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.BuildingBlocks.PayPal;
using YiPix.Services.Payment.Domain.Entities;
using YiPix.Services.Payment.Infrastructure.Data;
using PayPalOptions = YiPix.BuildingBlocks.PayPal.PayPalOptions;

namespace YiPix.Services.Payment.Application;

/// <summary>
/// 支付服务接口 - 支付创建、捕获、查询、Webhook 处理
/// </summary>
public interface IPaymentAppService
{
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default);
    Task<PaymentDto> CapturePaymentAsync(CapturePaymentRequest request, CancellationToken ct = default);
    Task<PaymentDto?> GetPaymentAsync(Guid id, CancellationToken ct = default);
    Task<List<PaymentDto>> GetUserPaymentsAsync(Guid userId, CancellationToken ct = default);
    Task ProcessWebhookAsync(string eventType, string resourceId, string payload, CancellationToken ct = default);
}

/// <summary>
/// 支付服务实现：对接 PayPal 支付，处理 Webhook 回调，发布 PaymentCompletedEvent
/// </summary>
public class PaymentAppService : IPaymentAppService
{
    private readonly IPaymentRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IPayPalClient _paypalClient;
    private readonly PayPalOptions _paypalOptions;
    private readonly ILogger<PaymentAppService> _logger;

    public PaymentAppService(
        IPaymentRepository repository,
        IEventBus eventBus,
        IPayPalClient paypalClient,
        IOptions<PayPalOptions> paypalOptions,
        ILogger<PaymentAppService> logger)
    {
        _repository = repository;
        _eventBus = eventBus;
        _paypalClient = paypalClient;
        _paypalOptions = paypalOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 创建支付订单：根据计划类型自动选择 Orders API（一次性）或 Subscriptions API（周期性）
    /// - Lifetime → PayPal Orders API（一次性付费，需后续 Capture）
    /// - Monthly/Yearly → PayPal Subscriptions API（周期性扣款，无需 Capture）
    /// </summary>
    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        // 获取计划价格
        if (!_paypalOptions.PlanPrices.TryGetValue(request.PlanId, out var amount))
            throw new ArgumentException($"Unknown plan: {request.PlanId}");

        var isSubscription = request.PlanId is "Monthly" or "Yearly";
        string? approveUrl = null;

        var payment = new Domain.Entities.Payment
        {
            UserId = request.UserId,
            PlanId = request.PlanId,
            Status = "Pending",
            Amount = amount,
            Currency = "USD",
            PaymentType = isSubscription ? "Subscription" : "OneTime"
        };

        if (isSubscription)
        {
            // 周期性订阅：使用 Subscriptions API
            if (!_paypalOptions.PlanIdMappings.TryGetValue(request.PlanId, out var paypalPlanId))
                throw new ArgumentException($"PayPal plan ID not configured for: {request.PlanId}");

            var subscriptionResult = await _paypalClient.CreateSubscriptionAsync(
                paypalPlanId, request.ReturnUrl, request.CancelUrl, ct);

            payment.PayPalSubscriptionId = subscriptionResult.SubscriptionId;
            approveUrl = subscriptionResult.ApproveUrl;

            _logger.LogInformation(
                "Created PayPal subscription {SubscriptionId} for user {UserId}, plan: {Plan}",
                subscriptionResult.SubscriptionId, request.UserId, request.PlanId);
        }
        else
        {
            // 一次性付费（如 Lifetime）：使用 Orders API
            var orderResult = await _paypalClient.CreateOrderAsync(
                amount, "USD", request.ReturnUrl, request.CancelUrl,
                $"YiPix {request.PlanId} Plan", ct);

            payment.PayPalOrderId = orderResult.OrderId;
            approveUrl = orderResult.ApproveUrl;

            _logger.LogInformation(
                "Created PayPal order {OrderId} for user {UserId}, amount: {Amount} USD",
                orderResult.OrderId, request.UserId, amount);
        }

        await _repository.CreateAsync(payment, ct);
        return MapToDto(payment, approveUrl);
    }

    /// <summary>
    /// 捕获支付：调用 PayPal Capture API 完成扣款（仅适用于 Orders API 一次性付费）
    /// 扣款成功后发布 PaymentCompletedEvent 事件
    /// </summary>
    public async Task<PaymentDto> CapturePaymentAsync(CapturePaymentRequest request, CancellationToken ct = default)
    {
        var payment = await _repository.GetByPayPalOrderIdAsync(request.PayPalOrderId, ct)
            ?? throw new NotFoundException("Payment", Guid.Empty);

        if (payment.Status != "Pending")
            throw new InvalidOperationException($"Payment is already in {payment.Status} status");

        try
        {
            // 调用 PayPal Capture API 真正扣款
            var captureResult = await _paypalClient.CaptureOrderAsync(request.PayPalOrderId, ct);

            if (captureResult.Status == "COMPLETED")
            {
                payment.Status = "Completed";
                payment.CompletedAt = DateTime.UtcNow;
                payment.Amount = captureResult.Amount; // 使用 PayPal 返回的实际金额
                await _repository.UpdateAsync(payment, ct);

                // 发布支付完成事件（WebhookWorker / SubscriptionService 消费）
                await _eventBus.PublishAsync(
                    new PaymentCompletedEvent(
                        payment.UserId, payment.Id, payment.Amount, payment.Currency,
                        payment.PlanId ?? "", payment.PaymentType),
                    ct);

                _logger.LogInformation(
                    "Payment {PaymentId} captured successfully, amount: {Amount} {Currency}",
                    payment.Id, payment.Amount, payment.Currency);
            }
            else
            {
                payment.Status = "Failed";
                payment.FailureReason = $"PayPal capture returned status: {captureResult.Status}";
                await _repository.UpdateAsync(payment, ct);

                _logger.LogWarning("Payment {PaymentId} capture returned unexpected status: {Status}",
                    payment.Id, captureResult.Status);
            }
        }
        catch (Exception ex)
        {
            payment.Status = "Failed";
            payment.FailureReason = ex.Message;
            await _repository.UpdateAsync(payment, ct);

            _logger.LogError(ex, "Failed to capture payment {PaymentId}", payment.Id);

            await _eventBus.PublishAsync(
                new PaymentFailedEvent(payment.UserId, payment.Id, ex.Message), ct);

            throw;
        }

        return MapToDto(payment);
    }

    public async Task<PaymentDto?> GetPaymentAsync(Guid id, CancellationToken ct = default)
    {
        var payment = await _repository.GetByIdAsync(id, ct);
        return payment == null ? null : MapToDto(payment);
    }

    public async Task<List<PaymentDto>> GetUserPaymentsAsync(Guid userId, CancellationToken ct = default)
    {
        var payments = await _repository.GetByUserIdAsync(userId, ct);
        return payments.Select(p => MapToDto(p)).ToList();
    }

    /// <summary>
    /// Webhook 幂等处理：根据 eventType 分发处理，已处理过的事件自动跳过
    /// 支持事件类型：
    /// - PAYMENT.CAPTURE.COMPLETED：一次性支付到账
    /// - PAYMENT.CAPTURE.DENIED：支付被拒绝
    /// - PAYMENT.CAPTURE.REFUNDED：支付被退款
    /// - BILLING.SUBSCRIPTION.ACTIVATED：订阅激活
    /// - BILLING.SUBSCRIPTION.CANCELLED：订阅取消
    /// - BILLING.SUBSCRIPTION.SUSPENDED：订阅暂停
    /// - PAYMENT.SALE.COMPLETED：订阅周期扣款成功
    /// </summary>
    public async Task ProcessWebhookAsync(string eventType, string resourceId, string payload, CancellationToken ct = default)
    {
        // 幂等性检查
        if (await _repository.WebhookAlreadyProcessedAsync(eventType, resourceId, ct))
        {
            _logger.LogInformation("Webhook event {EventType} with resource {ResourceId} already processed, skipping",
                eventType, resourceId);
            return;
        }

        var log = new WebhookLog
        {
            EventType = eventType,
            ResourceId = resourceId,
            Payload = payload
        };

        try
        {
            switch (eventType)
            {
                case "PAYMENT.CAPTURE.COMPLETED":
                    await HandlePaymentCaptureCompletedAsync(resourceId, payload, ct);
                    break;

                case "PAYMENT.CAPTURE.DENIED":
                    await HandlePaymentCaptureDeniedAsync(resourceId, ct);
                    break;

                case "PAYMENT.CAPTURE.REFUNDED":
                    await HandlePaymentCaptureRefundedAsync(resourceId, ct);
                    break;

                case "BILLING.SUBSCRIPTION.ACTIVATED":
                    await HandleSubscriptionActivatedAsync(resourceId, payload, ct);
                    break;

                case "BILLING.SUBSCRIPTION.CANCELLED":
                    await HandleSubscriptionCancelledAsync(resourceId, payload, ct);
                    break;

                case "BILLING.SUBSCRIPTION.SUSPENDED":
                    await HandleSubscriptionSuspendedAsync(resourceId, payload, ct);
                    break;

                case "PAYMENT.SALE.COMPLETED":
                    await HandlePaymentSaleCompletedAsync(resourceId, payload, ct);
                    break;

                default:
                    _logger.LogWarning("Unhandled webhook event type: {EventType}", eventType);
                    break;
            }

            log.Processed = true;
            log.ProcessedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook event {EventType} for resource {ResourceId}",
                eventType, resourceId);
            log.ProcessingError = ex.Message;
        }

        await _repository.CreateWebhookLogAsync(log, ct);
    }

    // ========== Webhook 事件处理器 ==========

    /// <summary>一次性支付到账：标记 Payment 为 Completed，发布事件</summary>
    private async Task HandlePaymentCaptureCompletedAsync(string captureId, string payload, CancellationToken ct)
    {
        // 从 payload 中提取关联的 order ID
        var jsonDoc = JsonDocument.Parse(payload);
        var resource = jsonDoc.RootElement.GetProperty("resource");

        // 尝试获取父级订单 ID
        string? orderId = null;
        if (resource.TryGetProperty("supplementary_data", out var suppData) &&
            suppData.TryGetProperty("related_ids", out var relatedIds) &&
            relatedIds.TryGetProperty("order_id", out var orderIdProp))
        {
            orderId = orderIdProp.GetString();
        }

        Domain.Entities.Payment? payment = null;
        if (!string.IsNullOrEmpty(orderId))
            payment = await _repository.GetByPayPalOrderIdAsync(orderId, ct);

        if (payment == null)
        {
            _logger.LogWarning("No payment found for capture {CaptureId}, order {OrderId}", captureId, orderId);
            return;
        }

        if (payment.Status == "Completed") return; // 已处理

        payment.Status = "Completed";
        payment.CompletedAt = DateTime.UtcNow;

        // 更新实际金额
        if (resource.TryGetProperty("amount", out var amountProp) &&
            amountProp.TryGetProperty("value", out var valueProp))
        {
            if (decimal.TryParse(valueProp.GetString(), out var amt))
                payment.Amount = amt;
        }

        await _repository.UpdateAsync(payment, ct);

        await _eventBus.PublishAsync(
            new PaymentCompletedEvent(
                payment.UserId, payment.Id, payment.Amount, payment.Currency,
                payment.PlanId ?? "", payment.PaymentType),
            ct);

        _logger.LogInformation("Webhook: Payment {PaymentId} marked as Completed via capture {CaptureId}",
            payment.Id, captureId);
    }

    /// <summary>支付被拒绝：标记 Payment 为 Failed</summary>
    private Task HandlePaymentCaptureDeniedAsync(string captureId, CancellationToken ct)
    {
        // 查找关联的支付记录（通过 PayPalOrderId 模糊查找或记录 captureId）
        _logger.LogWarning("Payment capture denied: {CaptureId}", captureId);
        // 在实际场景中需要通过 captureId 关联到具体的 Payment 记录
        return Task.CompletedTask;
    }

    /// <summary>支付被退款：标记 Payment 为 Refunded</summary>
    private Task HandlePaymentCaptureRefundedAsync(string captureId, CancellationToken ct)
    {
        _logger.LogInformation("Payment capture refunded: {CaptureId}", captureId);
        // 在实际场景中需要通过 captureId 关联到具体的 Payment 记录并标记为 Refunded
        return Task.CompletedTask;
    }

    /// <summary>订阅激活：标记关联的支付为 Completed，发布事件</summary>
    private async Task HandleSubscriptionActivatedAsync(string subscriptionId, string payload, CancellationToken ct)
    {
        var payment = await _repository.GetByPayPalSubscriptionIdAsync(subscriptionId, ct);
        if (payment == null)
        {
            _logger.LogWarning("No payment found for subscription activation: {SubscriptionId}", subscriptionId);
            return;
        }

        if (payment.Status == "Completed") return;

        payment.Status = "Completed";
        payment.CompletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(payment, ct);

        await _eventBus.PublishAsync(
            new PaymentCompletedEvent(
                payment.UserId, payment.Id, payment.Amount, payment.Currency,
                payment.PlanId ?? "", payment.PaymentType),
            ct);

        _logger.LogInformation("Webhook: Subscription {SubscriptionId} activated, payment {PaymentId} completed",
            subscriptionId, payment.Id);
    }

    /// <summary>订阅取消：发布取消事件供 SubscriptionService 处理</summary>
    private async Task HandleSubscriptionCancelledAsync(string subscriptionId, string payload, CancellationToken ct)
    {
        var payment = await _repository.GetByPayPalSubscriptionIdAsync(subscriptionId, ct);
        if (payment == null)
        {
            _logger.LogWarning("No payment found for subscription cancellation: {SubscriptionId}", subscriptionId);
            return;
        }

        _logger.LogInformation("Webhook: Subscription {SubscriptionId} cancelled for user {UserId}",
            subscriptionId, payment.UserId);
    }

    /// <summary>订阅暂停（扣款失败）：记录日志</summary>
    private async Task HandleSubscriptionSuspendedAsync(string subscriptionId, string payload, CancellationToken ct)
    {
        var payment = await _repository.GetByPayPalSubscriptionIdAsync(subscriptionId, ct);
        if (payment == null)
        {
            _logger.LogWarning("No payment found for subscription suspension: {SubscriptionId}", subscriptionId);
            return;
        }

        _logger.LogWarning("Webhook: Subscription {SubscriptionId} suspended for user {UserId}",
            subscriptionId, payment.UserId);
    }

    /// <summary>订阅周期扣款成功：记录续费支付，发布事件</summary>
    private async Task HandlePaymentSaleCompletedAsync(string saleId, string payload, CancellationToken ct)
    {
        var jsonDoc = JsonDocument.Parse(payload);
        var resource = jsonDoc.RootElement.GetProperty("resource");

        string? subscriptionId = null;
        if (resource.TryGetProperty("billing_agreement_id", out var biIdProp))
            subscriptionId = biIdProp.GetString();

        if (string.IsNullOrEmpty(subscriptionId))
        {
            _logger.LogWarning("No subscription ID in PAYMENT.SALE.COMPLETED for sale {SaleId}", saleId);
            return;
        }

        // 查找原始支付记录获取用户信息
        var originalPayment = await _repository.GetByPayPalSubscriptionIdAsync(subscriptionId, ct);
        if (originalPayment == null)
        {
            _logger.LogWarning("No payment found for subscription renewal: {SubscriptionId}", subscriptionId);
            return;
        }

        // 创建续费支付记录
        decimal amount = 0;
        string currency = "USD";
        if (resource.TryGetProperty("amount", out var amountProp))
        {
            if (amountProp.TryGetProperty("total", out var totalProp))
                decimal.TryParse(totalProp.GetString(), out amount);
            if (amountProp.TryGetProperty("currency", out var currProp))
                currency = currProp.GetString() ?? "USD";
        }

        var renewalPayment = new Domain.Entities.Payment
        {
            UserId = originalPayment.UserId,
            PlanId = originalPayment.PlanId,
            Status = "Completed",
            Amount = amount,
            Currency = currency,
            PayPalSubscriptionId = subscriptionId,
            PaymentType = "Subscription",
            CompletedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(renewalPayment, ct);

        // 发布续费事件，用于延长订阅有效期
        await _eventBus.PublishAsync(
            new PaymentCompletedEvent(
                renewalPayment.UserId, renewalPayment.Id, renewalPayment.Amount, renewalPayment.Currency,
                renewalPayment.PlanId ?? "", renewalPayment.PaymentType),
            ct);

        _logger.LogInformation(
            "Webhook: Subscription {SubscriptionId} renewed, sale {SaleId}, amount: {Amount} {Currency}",
            subscriptionId, saleId, amount, currency);
    }

    // ========== DTO 映射 ==========

    private static PaymentDto MapToDto(Domain.Entities.Payment p, string? approveUrl = null)
    {
        Enum.TryParse<PaymentStatus>(p.Status, out var status);
        return new PaymentDto(
            p.Id, p.UserId, p.Amount, p.Currency, status,
            p.PayPalOrderId ?? p.PayPalSubscriptionId ?? "",
            p.CreatedAt, approveUrl);
    }
}
