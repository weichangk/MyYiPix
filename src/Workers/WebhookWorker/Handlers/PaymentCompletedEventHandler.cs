using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;

namespace YiPix.Workers.Webhook.Handlers;

/// <summary>
/// WebhookWorker 中的支付完成事件处理器
/// 负责日志记录和辅助任务（订阅激活由 SubscriptionService 自行处理）
/// </summary>
public class PaymentCompletedEventHandler : IIntegrationEventHandler<PaymentCompletedEvent>
{
    private readonly ILogger<PaymentCompletedEventHandler> _logger;

    public PaymentCompletedEventHandler(ILogger<PaymentCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(PaymentCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Payment completed: PaymentId={PaymentId}, Amount={Amount} {Currency}, User={UserId}, Plan={PlanId}, Type={PaymentType}",
            @event.PaymentId, @event.Amount, @event.Currency, @event.UserId, @event.PlanId, @event.PaymentType);

        // 订阅激活/续期已由 SubscriptionService 的 PaymentCompletedEventHandler 处理
        // 此处仅负责辅助任务

        // TODO: 发送支付确认邮件
        _logger.LogInformation("TODO: Send payment confirmation email to user {UserId}", @event.UserId);

        // TODO: 记录分析统计事件
        _logger.LogInformation("TODO: Track analytics event for payment {PaymentId}", @event.PaymentId);

        await Task.CompletedTask;
    }
}
