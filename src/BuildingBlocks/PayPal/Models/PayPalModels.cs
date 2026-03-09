using System.Text.Json.Serialization;

namespace YiPix.BuildingBlocks.PayPal.Models;

// ========== Orders API Models ==========

/// <summary>创建 PayPal 订单的响应</summary>
public class CreateOrderResponse
{
    /// <summary>PayPal 订单 ID</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>订单状态（如 CREATED）</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>用户授权支付的 URL（重定向到此地址完成支付）</summary>
    public string ApproveUrl { get; set; } = string.Empty;
}

/// <summary>捕获 PayPal 订单的响应</summary>
public class CaptureOrderResponse
{
    /// <summary>PayPal 订单 ID</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>订单状态（如 COMPLETED）</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>捕获 ID</summary>
    public string CaptureId { get; set; } = string.Empty;

    /// <summary>实际扣款金额</summary>
    public decimal Amount { get; set; }

    /// <summary>货币</summary>
    public string Currency { get; set; } = "USD";
}

// ========== Subscriptions API Models ==========

/// <summary>创建 PayPal 订阅的响应</summary>
public class CreateSubscriptionResponse
{
    /// <summary>PayPal 订阅 ID</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>订阅状态（如 APPROVAL_PENDING）</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>用户授权订阅的 URL</summary>
    public string ApproveUrl { get; set; } = string.Empty;
}

/// <summary>PayPal 订阅详情</summary>
public class SubscriptionDetailResponse
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? NextBillingTime { get; set; }
}

// ========== Webhook Models ==========

/// <summary>PayPal Webhook 签名验证请求体</summary>
public class WebhookVerifyRequest
{
    [JsonPropertyName("auth_algo")]
    public string AuthAlgo { get; set; } = string.Empty;

    [JsonPropertyName("cert_url")]
    public string CertUrl { get; set; } = string.Empty;

    [JsonPropertyName("transmission_id")]
    public string TransmissionId { get; set; } = string.Empty;

    [JsonPropertyName("transmission_sig")]
    public string TransmissionSig { get; set; } = string.Empty;

    [JsonPropertyName("transmission_time")]
    public string TransmissionTime { get; set; } = string.Empty;

    [JsonPropertyName("webhook_id")]
    public string WebhookId { get; set; } = string.Empty;

    [JsonPropertyName("webhook_event")]
    public object WebhookEvent { get; set; } = new();
}

/// <summary>PayPal Webhook 签名验证响应</summary>
public class WebhookVerifyResponse
{
    [JsonPropertyName("verification_status")]
    public string VerificationStatus { get; set; } = string.Empty;
}

// ========== PayPal API 原始 JSON 模型（用于序列化/反序列化） ==========

public class PayPalOrderRequest
{
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "CAPTURE";

    [JsonPropertyName("purchase_units")]
    public List<PurchaseUnit> PurchaseUnits { get; set; } = [];

    [JsonPropertyName("payment_source")]
    public PaymentSource? PaymentSource { get; set; }
}

public class PurchaseUnit
{
    [JsonPropertyName("amount")]
    public AmountInfo Amount { get; set; } = new();

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class AmountInfo
{
    [JsonPropertyName("currency_code")]
    public string CurrencyCode { get; set; } = "USD";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "0.00";
}

public class PaymentSource
{
    [JsonPropertyName("paypal")]
    public PayPalSource? PayPal { get; set; }
}

public class PayPalSource
{
    [JsonPropertyName("experience_context")]
    public ExperienceContext? ExperienceContext { get; set; }
}

public class ExperienceContext
{
    [JsonPropertyName("return_url")]
    public string ReturnUrl { get; set; } = string.Empty;

    [JsonPropertyName("cancel_url")]
    public string CancelUrl { get; set; } = string.Empty;

    [JsonPropertyName("user_action")]
    public string UserAction { get; set; } = "PAY_NOW";
}

public class PayPalOrderResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("links")]
    public List<LinkInfo> Links { get; set; } = [];
}

public class LinkInfo
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("rel")]
    public string Rel { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string? Method { get; set; }
}

public class PayPalCaptureResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("purchase_units")]
    public List<CapturedPurchaseUnit>? PurchaseUnits { get; set; }
}

public class CapturedPurchaseUnit
{
    [JsonPropertyName("payments")]
    public CapturedPayments? Payments { get; set; }
}

public class CapturedPayments
{
    [JsonPropertyName("captures")]
    public List<CaptureDetail>? Captures { get; set; }
}

public class CaptureDetail
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public AmountInfo? Amount { get; set; }
}

public class PayPalSubscriptionRequest
{
    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("application_context")]
    public SubscriptionAppContext ApplicationContext { get; set; } = new();
}

public class SubscriptionAppContext
{
    [JsonPropertyName("return_url")]
    public string ReturnUrl { get; set; } = string.Empty;

    [JsonPropertyName("cancel_url")]
    public string CancelUrl { get; set; } = string.Empty;

    [JsonPropertyName("user_action")]
    public string UserAction { get; set; } = "SUBSCRIBE_NOW";
}

public class PayPalSubscriptionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("plan_id")]
    public string? PlanId { get; set; }

    [JsonPropertyName("start_time")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("links")]
    public List<LinkInfo> Links { get; set; } = [];
}

public class PayPalSubscriptionDetailResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("start_time")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("billing_info")]
    public BillingInfo? BillingInfo { get; set; }
}

public class BillingInfo
{
    [JsonPropertyName("next_billing_time")]
    public DateTime? NextBillingTime { get; set; }
}

public class PayPalCancelSubscriptionRequest
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class PayPalTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
