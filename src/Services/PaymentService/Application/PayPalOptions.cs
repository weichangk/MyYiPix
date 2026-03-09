namespace YiPix.Services.Payment.Application;

/// <summary>
/// PayPal REST API 配置选项
/// </summary>
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    /// <summary>PayPal REST API 应用标识（绑定收款商家账号）</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>PayPal REST API 密钥（与 ClientId 配对使用）</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>API 地址（Sandbox: api-m.sandbox.paypal.com，Live: api-m.paypal.com）</summary>
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";

    /// <summary>Webhook 配置 ID（用于验证 Webhook 签名的真实性）</summary>
    public string WebhookId { get; set; } = string.Empty;
}
