using YiPix.BuildingBlocks.PayPal.Models;

namespace YiPix.BuildingBlocks.PayPal;

/// <summary>
/// PayPal REST API 客户端接口
/// 封装 Orders API（一次性付费）和 Subscriptions API（周期性订阅）
/// </summary>
public interface IPayPalClient
{
    // ========== Orders API（一次性付费） ==========

    /// <summary>
    /// 创建 PayPal 订单（用户授权前的第一步）
    /// </summary>
    /// <param name="amount">金额</param>
    /// <param name="currency">货币类型（默认 USD）</param>
    /// <param name="returnUrl">用户授权后回调 URL</param>
    /// <param name="cancelUrl">用户取消后回调 URL</param>
    /// <param name="description">订单描述</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含 OrderId 和 ApproveUrl 的响应</returns>
    Task<CreateOrderResponse> CreateOrderAsync(decimal amount, string currency,
        string returnUrl, string cancelUrl, string? description = null, CancellationToken ct = default);

    /// <summary>
    /// 捕获（确认扣款）PayPal 订单（用户授权后的第二步）
    /// </summary>
    /// <param name="orderId">PayPal Order ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>捕获结果</returns>
    Task<CaptureOrderResponse> CaptureOrderAsync(string orderId, CancellationToken ct = default);

    // ========== Subscriptions API（周期性订阅） ==========

    /// <summary>
    /// 创建 PayPal 订阅（用户需跳转 PayPal 授权）
    /// </summary>
    /// <param name="planId">PayPal Plan ID（在 PayPal 后台预先创建）</param>
    /// <param name="returnUrl">用户授权后回调 URL</param>
    /// <param name="cancelUrl">用户取消后回调 URL</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含 SubscriptionId 和 ApproveUrl 的响应</returns>
    Task<CreateSubscriptionResponse> CreateSubscriptionAsync(string planId,
        string returnUrl, string cancelUrl, CancellationToken ct = default);

    /// <summary>
    /// 取消 PayPal 订阅
    /// </summary>
    /// <param name="subscriptionId">PayPal Subscription ID</param>
    /// <param name="reason">取消原因</param>
    /// <param name="ct">取消令牌</param>
    Task CancelSubscriptionAsync(string subscriptionId, string reason, CancellationToken ct = default);

    /// <summary>
    /// 获取 PayPal 订阅详情
    /// </summary>
    /// <param name="subscriptionId">PayPal Subscription ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>订阅详情</returns>
    Task<SubscriptionDetailResponse> GetSubscriptionDetailAsync(string subscriptionId, CancellationToken ct = default);

    // ========== Webhook 验证 ==========

    /// <summary>
    /// 验证 PayPal Webhook 签名
    /// </summary>
    /// <param name="webhookId">Webhook 配置 ID</param>
    /// <param name="headers">请求头（包含签名信息）</param>
    /// <param name="body">原始请求体</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>签名是否合法</returns>
    Task<bool> VerifyWebhookSignatureAsync(string webhookId,
        IDictionary<string, string> headers, string body, CancellationToken ct = default);
}
