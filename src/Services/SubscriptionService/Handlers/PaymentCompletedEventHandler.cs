using Microsoft.Extensions.Logging;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.Services.Subscription.Application;

namespace YiPix.Services.Subscription.Handlers;

/// <summary>
/// 支付完成事件处理器 - 在 SubscriptionService 中消费 PaymentCompletedEvent
/// 支付完成后自动创建或续期用户订阅
/// </summary>
public class PaymentCompletedEventHandler : IIntegrationEventHandler<PaymentCompletedEvent>
{
    private readonly ISubscriptionAppService _subscriptionService;
    private readonly ILogger<PaymentCompletedEventHandler> _logger;

    public PaymentCompletedEventHandler(
        ISubscriptionAppService subscriptionService,
        ILogger<PaymentCompletedEventHandler> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling PaymentCompletedEvent: PaymentId={PaymentId}, UserId={UserId}, Plan={PlanId}, Amount={Amount} {Currency}",
            @event.PaymentId, @event.UserId, @event.PlanId, @event.Amount, @event.Currency);

        try
        {
            // 根据支付完成事件激活或续期订阅
            await _subscriptionService.ActivateOrRenewByPaymentAsync(
                @event.UserId, @event.PlanId, @event.PaymentType, cancellationToken);

            _logger.LogInformation(
                "Subscription activated/renewed for user {UserId} via payment {PaymentId}",
                @event.UserId, @event.PaymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to activate/renew subscription for user {UserId} via payment {PaymentId}",
                @event.UserId, @event.PaymentId);
            throw; // 抛出异常使消息重入队列重试
        }
    }
}
