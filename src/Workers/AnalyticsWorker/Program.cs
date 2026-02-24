using YiPix.BuildingBlocks.EventBus;
using YiPix.BuildingBlocks.Logging;
using YiPix.Workers.Analytics;
using YiPix.Workers.Analytics.Handlers;

var host = Host.CreateDefaultBuilder(args)
    .UseYiPixSerilog("AnalyticsWorker")
    .ConfigureServices((context, services) =>
    {
        services.AddRabbitMQEventBus(
            context.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");
        services.AddScoped<DownloadEventHandler>();
        services.AddHostedService<AnalyticsWorker>();
    })
    .Build();

host.Run();
