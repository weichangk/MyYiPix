using YiPix.BuildingBlocks.EventBus;
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

host.Run();
