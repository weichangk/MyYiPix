namespace YiPix.Workers.Webhook;

/// <summary>
/// Webhook Worker 后台服务
/// 事件消费通过 RabbitMQ EventBus 的 Subscribe 机制自动完成，
/// 此 Worker 作为保活进程运行，并可扩展轮询等辅助逻辑
/// </summary>
public class WebhookWorker : BackgroundService
{
    private readonly ILogger<WebhookWorker> _logger;

    public WebhookWorker(ILogger<WebhookWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook Worker started at: {Time}", DateTimeOffset.Now);
        _logger.LogInformation("Event subscriptions are active via RabbitMQ EventBus");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 事件消费由 RabbitMQ EventBus Subscribe 机制自动驱动
                // Worker 进程保活，后续可扩展以下功能：
                // - 定期检查未处理的 Webhook 日志并重试
                // - 清理过期的 Webhook 日志
                // - 健康检查上报

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Webhook Worker processing loop");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Webhook Worker stopped at: {Time}", DateTimeOffset.Now);
    }
}
