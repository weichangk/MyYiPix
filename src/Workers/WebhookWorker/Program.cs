using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.BuildingBlocks.Logging;
using YiPix.Workers.Webhook;
using YiPix.Workers.Webhook.Handlers;

var host = Host.CreateDefaultBuilder(args)
    .UseYiPixSerilog("WebhookWorker")
    .ConfigureServices((context, services) =>
    {
        services.AddRabbitMQEventBus(
            context.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");
        services.AddScoped<PaymentCompletedEventHandler>();
        services.AddHostedService<WebhookWorker>();
    })
    .Build();

// 订阅事件
var eventBus = host.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<PaymentCompletedEvent, PaymentCompletedEventHandler>();

host.Run();
