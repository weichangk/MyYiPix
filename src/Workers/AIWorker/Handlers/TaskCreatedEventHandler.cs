using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus.Abstractions;

namespace YiPix.Workers.AI.Handlers;

public class TaskCreatedEventHandler : IIntegrationEventHandler<TaskCreatedEvent>
{
    private readonly ILogger<TaskCreatedEventHandler> _logger;

    public TaskCreatedEventHandler(ILogger<TaskCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(TaskCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.TaskType != "AIEnhance")
            return;

        _logger.LogInformation("Processing AI task {TaskId} for user {UserId}", @event.TaskId, @event.UserId);

        // TODO: Implement AI image enhancement logic
        // 1. Download input file
        // 2. Run AI processing
        // 3. Upload output file  
        // 4. Update task status

        await Task.CompletedTask;
    }
}
