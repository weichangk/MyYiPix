using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;

namespace YiPix.Workers.Webhook.Handlers;

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
            "Payment completed: {PaymentId}, Amount: {Amount} {Currency} for User: {UserId}",
            @event.PaymentId, @event.Amount, @event.Currency, @event.UserId);

        // TODO: Activate or renew subscription
        // TODO: Send confirmation email
        // TODO: Track analytics event

        await Task.CompletedTask;
    }
}
