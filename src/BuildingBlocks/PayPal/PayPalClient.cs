using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YiPix.BuildingBlocks.PayPal.Models;

namespace YiPix.BuildingBlocks.PayPal;

/// <summary>
/// PayPal REST API 客户端实现
/// 封装 OAuth2 Token 获取与缓存、Orders API、Subscriptions API、Webhook 签名验证
/// </summary>
public class PayPalClient : IPayPalClient
{
    private readonly HttpClient _httpClient;
    private readonly PayPalOptions _options;
    private readonly ILogger<PayPalClient> _logger;

    // Token 缓存
    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public PayPalClient(HttpClient httpClient, IOptions<PayPalOptions> options, ILogger<PayPalClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    // ========== Orders API ==========

    public async Task<CreateOrderResponse> CreateOrderAsync(decimal amount, string currency,
        string returnUrl, string cancelUrl, string? description = null, CancellationToken ct = default)
    {
        var orderRequest = new PayPalOrderRequest
        {
            Intent = "CAPTURE",
            PurchaseUnits =
            [
                new PurchaseUnit
                {
                    Amount = new AmountInfo
                    {
                        CurrencyCode = currency,
                        Value = amount.ToString("F2", CultureInfo.InvariantCulture)
                    },
                    Description = description
                }
            ],
            PaymentSource = new PaymentSource
            {
                PayPal = new PayPalSource
                {
                    ExperienceContext = new ExperienceContext
                    {
                        ReturnUrl = returnUrl,
                        CancelUrl = cancelUrl,
                        UserAction = "PAY_NOW"
                    }
                }
            }
        };

        var response = await SendAuthorizedRequestAsync<PayPalOrderResponse>(
            HttpMethod.Post, "v2/checkout/orders", orderRequest, ct);

        var approveUrl = response.Links.FirstOrDefault(l => l.Rel == "payer-action")?.Href
                      ?? response.Links.FirstOrDefault(l => l.Rel == "approve")?.Href
                      ?? string.Empty;

        _logger.LogInformation("Created PayPal order {OrderId}, status: {Status}", response.Id, response.Status);

        return new CreateOrderResponse
        {
            OrderId = response.Id,
            Status = response.Status,
            ApproveUrl = approveUrl
        };
    }

    public async Task<CaptureOrderResponse> CaptureOrderAsync(string orderId, CancellationToken ct = default)
    {
        var response = await SendAuthorizedRequestAsync<PayPalCaptureResponse>(
            HttpMethod.Post, $"v2/checkout/orders/{orderId}/capture", null, ct);

        var capture = response.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?.FirstOrDefault();

        _logger.LogInformation("Captured PayPal order {OrderId}, status: {Status}", response.Id, response.Status);

        return new CaptureOrderResponse
        {
            OrderId = response.Id,
            Status = response.Status,
            CaptureId = capture?.Id ?? string.Empty,
            Amount = decimal.TryParse(capture?.Amount?.Value, CultureInfo.InvariantCulture, out var amt) ? amt : 0,
            Currency = capture?.Amount?.CurrencyCode ?? "USD"
        };
    }

    // ========== Subscriptions API ==========

    public async Task<CreateSubscriptionResponse> CreateSubscriptionAsync(string planId,
        string returnUrl, string cancelUrl, CancellationToken ct = default)
    {
        var subscriptionRequest = new PayPalSubscriptionRequest
        {
            PlanId = planId,
            ApplicationContext = new SubscriptionAppContext
            {
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl,
                UserAction = "SUBSCRIBE_NOW"
            }
        };

        var response = await SendAuthorizedRequestAsync<PayPalSubscriptionResponse>(
            HttpMethod.Post, "v1/billing/subscriptions", subscriptionRequest, ct);

        var approveUrl = response.Links.FirstOrDefault(l => l.Rel == "approve")?.Href ?? string.Empty;

        _logger.LogInformation("Created PayPal subscription {SubscriptionId}, status: {Status}",
            response.Id, response.Status);

        return new CreateSubscriptionResponse
        {
            SubscriptionId = response.Id,
            Status = response.Status,
            ApproveUrl = approveUrl
        };
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, string reason, CancellationToken ct = default)
    {
        var cancelRequest = new PayPalCancelSubscriptionRequest { Reason = reason };

        await SendAuthorizedRequestAsync(
            HttpMethod.Post, $"v1/billing/subscriptions/{subscriptionId}/cancel", cancelRequest, ct);

        _logger.LogInformation("Cancelled PayPal subscription {SubscriptionId}, reason: {Reason}",
            subscriptionId, reason);
    }

    public async Task<SubscriptionDetailResponse> GetSubscriptionDetailAsync(string subscriptionId, CancellationToken ct = default)
    {
        var response = await SendAuthorizedRequestAsync<PayPalSubscriptionDetailResponse>(
            HttpMethod.Get, $"v1/billing/subscriptions/{subscriptionId}", null, ct);

        return new SubscriptionDetailResponse
        {
            SubscriptionId = response.Id,
            Status = response.Status,
            PlanId = response.PlanId,
            StartTime = response.StartTime,
            NextBillingTime = response.BillingInfo?.NextBillingTime
        };
    }

    // ========== Webhook 验证 ==========

    public async Task<bool> VerifyWebhookSignatureAsync(string webhookId,
        IDictionary<string, string> headers, string body, CancellationToken ct = default)
    {
        try
        {
            // 解析原始 body 为 JsonElement 用于嵌入验证请求
            var webhookEvent = JsonSerializer.Deserialize<JsonElement>(body);

            var verifyRequest = new WebhookVerifyRequest
            {
                AuthAlgo = GetHeaderValue(headers, "PAYPAL-AUTH-ALGO"),
                CertUrl = GetHeaderValue(headers, "PAYPAL-CERT-URL"),
                TransmissionId = GetHeaderValue(headers, "PAYPAL-TRANSMISSION-ID"),
                TransmissionSig = GetHeaderValue(headers, "PAYPAL-TRANSMISSION-SIG"),
                TransmissionTime = GetHeaderValue(headers, "PAYPAL-TRANSMISSION-TIME"),
                WebhookId = webhookId,
                WebhookEvent = webhookEvent
            };

            var response = await SendAuthorizedRequestAsync<WebhookVerifyResponse>(
                HttpMethod.Post, "v1/notifications/verify-webhook-signature", verifyRequest, ct);

            var isValid = response.VerificationStatus == "SUCCESS";
            _logger.LogInformation("Webhook signature verification: {Status}", response.VerificationStatus);
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify webhook signature");
            return false;
        }
    }

    // ========== 内部 HTTP 工具方法 ==========

    private static string GetHeaderValue(IDictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>
    /// 获取 PayPal OAuth2 Access Token（带缓存）
    /// </summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            // 缓存未过期则直接使用（提前 60 秒刷新避免竞态）
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiresAt.AddSeconds(-60))
                return _cachedToken;

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token")
            {
                Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                ])
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var tokenResponse = await JsonSerializer.DeserializeAsync<PayPalTokenResponse>(
                await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);

            _cachedToken = tokenResponse!.AccessToken;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            _logger.LogDebug("Obtained PayPal access token, expires in {ExpiresIn}s", tokenResponse.ExpiresIn);

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>发送授权请求并反序列化响应</summary>
    private async Task<TResponse> SendAuthorizedRequestAsync<TResponse>(
        HttpMethod method, string endpoint, object? payload, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);

        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (payload != null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request, ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal API error: {StatusCode} {Body} for {Method} {Endpoint}",
                response.StatusCode, responseBody, method, endpoint);
            throw new HttpRequestException(
                $"PayPal API returned {response.StatusCode}: {responseBody}");
        }

        return JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize PayPal response for {endpoint}");
    }

    /// <summary>发送授权请求（不需要响应体，如取消订阅返回 204）</summary>
    private async Task SendAuthorizedRequestAsync(
        HttpMethod method, string endpoint, object? payload, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);

        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (payload != null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("PayPal API error: {StatusCode} {Body} for {Method} {Endpoint}",
                response.StatusCode, responseBody, method, endpoint);
            throw new HttpRequestException(
                $"PayPal API returned {response.StatusCode}: {responseBody}");
        }
    }
}
