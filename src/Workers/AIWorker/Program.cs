using YiPix.BuildingBlocks.EventBus;
using YiPix.BuildingBlocks.Logging;
using YiPix.Workers.AI;
using YiPix.Workers.AI.Handlers;

var host = Host.CreateDefaultBuilder(args)
    .UseYiPixSerilog("AIWorker")
    .ConfigureServices((context, services) =>
    {
        services.AddRabbitMQEventBus(
            context.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");
        services.AddScoped<TaskCreatedEventHandler>();
        services.AddHostedService<AIWorker>();
    })
    .Build();

host.Run();
