using Microsoft.Extensions.DependencyInjection;

namespace YiPix.BuildingBlocks.PayPal;

/// <summary>
/// PayPal 服务注册扩展方法
/// </summary>
public static class PayPalServiceExtensions
{
    /// <summary>
    /// 注册 PayPal 客户端服务（包含 HttpClient 和配置绑定）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPayPalClient(
        this IServiceCollection services, Action<PayPalOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddHttpClient<IPayPalClient, PayPalClient>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }

    /// <summary>
    /// 使用配置节注册 PayPal 客户端服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置节（通常为 Configuration.GetSection("PayPal")）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPayPalClient(
        this IServiceCollection services, Microsoft.Extensions.Configuration.IConfigurationSection configuration)
    {
        services.Configure<PayPalOptions>(configuration);

        services.AddHttpClient<IPayPalClient, PayPalClient>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }
}
