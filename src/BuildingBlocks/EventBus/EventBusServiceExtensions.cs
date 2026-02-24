using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.BuildingBlocks.EventBus.RabbitMQ;

namespace YiPix.BuildingBlocks.EventBus;

public static class EventBusServiceExtensions
{
    public static IServiceCollection AddRabbitMQEventBus(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString)
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddSingleton<IEventBus, RabbitMQEventBus>();

        return services;
    }
}
