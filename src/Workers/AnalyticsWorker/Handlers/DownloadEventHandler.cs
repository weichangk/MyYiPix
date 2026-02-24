using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;

namespace YiPix.Workers.Analytics.Handlers;

public class DownloadEventHandler : IIntegrationEventHandler<DownloadStartedEvent>
{
    private readonly ILogger<DownloadEventHandler> _logger;

    public DownloadEventHandler(ILogger<DownloadEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(DownloadStartedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Download started: User {UserId}, Version {Version}, Platform {Platform}",
            @event.UserId, @event.Version, @event.Platform);

        // TODO: Increment download counter
        // TODO: Track platform distribution  
        // TODO: Update real-time dashboard metrics

        await Task.CompletedTask;
    }
}
