using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.Contracts.Payment;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.Services.Payment.Domain.Entities;
using YiPix.Services.Payment.Infrastructure.Data;

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

    public PaymentAppService(IPaymentRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        var payment = new Domain.Entities.Payment
        {
            UserId = request.UserId,
            PlanId = request.PlanId,
            Status = "Pending",
            Amount = 0, // Will be set after PayPal order creation
            PaymentType = "Subscription"
        };

        // TODO: Call PayPal API to create order/subscription
        // payment.PayPalOrderId = paypalResponse.OrderId;

        await _repository.CreateAsync(payment, ct);
        return MapToDto(payment);
    }

    public async Task<PaymentDto> CapturePaymentAsync(CapturePaymentRequest request, CancellationToken ct = default)
    {
        var payment = await _repository.GetByPayPalOrderIdAsync(request.PayPalOrderId, ct)
            ?? throw new NotFoundException("Payment", Guid.Empty);

        // TODO: Call PayPal API to capture payment
        payment.Status = "Completed";
        payment.CompletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(payment, ct);

        await _eventBus.PublishAsync(
            new PaymentCompletedEvent(payment.UserId, payment.Id, payment.Amount, payment.Currency), ct);

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
        return payments.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Webhook 幂等处理：根据 eventType 分发处理，已处理过的事件自动跳过
    /// </summary>
    public async Task ProcessWebhookAsync(string eventType, string resourceId, string payload, CancellationToken ct = default)
    {
        // 幂等性检查
        if (await _repository.WebhookAlreadyProcessedAsync(eventType, resourceId, ct))
            return;

        var log = new WebhookLog
        {
            EventType = eventType,
            ResourceId = resourceId,
            Payload = payload
        };

        try
        {
            // Process based on event type
            switch (eventType)
            {
                case "PAYMENT.CAPTURE.COMPLETED":
                    // Handle payment completion
                    break;
                case "BILLING.SUBSCRIPTION.ACTIVATED":
                    // Handle subscription activation
                    break;
                case "BILLING.SUBSCRIPTION.CANCELLED":
                    // Handle subscription cancellation
                    break;
            }

            log.Processed = true;
            log.ProcessedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            log.ProcessingError = ex.Message;
        }

        await _repository.CreateWebhookLogAsync(log, ct);
    }

    private static PaymentDto MapToDto(Domain.Entities.Payment p)
    {
        Enum.TryParse<PaymentStatus>(p.Status, out var status);
        return new PaymentDto(p.Id, p.UserId, p.Amount, p.Currency, status, p.PayPalOrderId ?? "", p.CreatedAt);
    }
}
