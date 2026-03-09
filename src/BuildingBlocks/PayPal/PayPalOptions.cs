namespace YiPix.BuildingBlocks.PayPal;

/// <summary>
/// PayPal REST API 配置选项
/// </summary>
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    /// <summary>PayPal REST API Client ID</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>PayPal REST API Client Secret</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>API Base URL（Sandbox: https://api-m.sandbox.paypal.com，Live: https://api-m.paypal.com）</summary>
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";

    /// <summary>Webhook ID（用于验证 Webhook 签名）</summary>
    public string WebhookId { get; set; } = string.Empty;

    /// <summary>
    /// 订阅计划 PayPal Plan ID 映射（内部计划名 → PayPal Plan ID）
    /// 例如: { "Monthly": "P-XXXXX", "Yearly": "P-YYYYY" }
    /// </summary>
    public Dictionary<string, string> PlanIdMappings { get; set; } = new();

    /// <summary>
    /// 计划价格映射（内部计划名 → 价格）
    /// 例如: { "Monthly": 9.90, "Yearly": 99.00, "Lifetime": 199.00 }
    /// </summary>
    public Dictionary<string, decimal> PlanPrices { get; set; } = new();
}
