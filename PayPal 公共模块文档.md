# PayPal 公共模块文档

## 模块概述

`YiPix.BuildingBlocks.PayPal` 是 YiPix 平台的 PayPal 支付集成公共模块，基于 .NET 9 实现，封装了 PayPal REST API 的核心功能：

- **Orders API**：一次性付费（买断制）
- **Subscriptions API**：周期性订阅（订阅制）
- **Webhook 验证**：安全的 Webhook 签名验证

## 项目结构

```
src/BuildingBlocks/PayPal/
├── IPayPalClient.cs              # PayPal 客户端接口定义
├── PayPalClient.cs               # PayPal 客户端实现
├── PayPalOptions.cs              # 配置选项类
├── PayPalServiceExtensions.cs    # 依赖注入扩展方法
├── Models/
│   └── PayPalModels.cs           # 请求/响应数据模型
└── YiPix.BuildingBlocks.PayPal.csproj
```

## 依赖项

| 包名 | 版本 | 用途 |
|------|------|------|
| Microsoft.Extensions.Http | 9.0.0 | HttpClient 工厂 |
| Microsoft.Extensions.Options | 9.0.0 | 配置绑定 |
| Microsoft.Extensions.Logging.Abstractions | 9.0.0 | 日志抽象 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.0 | DI 抽象 |

## 配置说明

### PayPalOptions 配置项

```csharp
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    // PayPal REST API 凭据
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }

    // API 基础 URL
    // Sandbox: https://api-m.sandbox.paypal.com
    // Live:    https://api-m.paypal.com
    public string BaseUrl { get; set; }

    // Webhook ID（用于验证签名）
    public string WebhookId { get; set; }

    // 订阅计划映射（内部名称 → PayPal Plan ID）
    public Dictionary<string, string> PlanIdMappings { get; set; }

    // 计划价格映射（内部名称 → 价格）
    public Dictionary<string, decimal> PlanPrices { get; set; }
}
```

### appsettings.json 示例

```json
{
  "PayPal": {
    "ClientId": "YOUR_PAYPAL_CLIENT_ID",
    "ClientSecret": "YOUR_PAYPAL_CLIENT_SECRET",
    "BaseUrl": "https://api-m.sandbox.paypal.com",
    "WebhookId": "YOUR_WEBHOOK_ID",
    "PlanIdMappings": {
      "Monthly": "P-XXXXXXXXXXXXXXXXXXXXXXXX",
      "Yearly": "P-YYYYYYYYYYYYYYYYYYYYYYYY"
    },
    "PlanPrices": {
      "Monthly": 9.90,
      "Yearly": 99.00,
      "Lifetime": 199.00
    }
  }
}
```

## 服务注册

### 方式一：使用配置节

```csharp
// Program.cs 或 Startup.cs
builder.Services.AddPayPalClient(
    builder.Configuration.GetSection(PayPalOptions.SectionName));
```

### 方式二：使用委托配置

```csharp
builder.Services.AddPayPalClient(options =>
{
    options.ClientId = "YOUR_CLIENT_ID";
    options.ClientSecret = "YOUR_CLIENT_SECRET";
    options.BaseUrl = "https://api-m.sandbox.paypal.com";
    options.WebhookId = "YOUR_WEBHOOK_ID";
});
```

## 接口说明

### IPayPalClient 接口

```csharp
public interface IPayPalClient
{
    // ========== Orders API（一次性付费） ==========
    
    /// <summary>创建 PayPal 订单</summary>
    Task<CreateOrderResponse> CreateOrderAsync(
        decimal amount, 
        string currency,
        string returnUrl, 
        string cancelUrl, 
        string? description = null, 
        CancellationToken ct = default);

    /// <summary>捕获（确认扣款）PayPal 订单</summary>
    Task<CaptureOrderResponse> CaptureOrderAsync(
        string orderId, 
        CancellationToken ct = default);

    // ========== Subscriptions API（周期性订阅） ==========
    
    /// <summary>创建 PayPal 订阅</summary>
    Task<CreateSubscriptionResponse> CreateSubscriptionAsync(
        string planId,
        string returnUrl, 
        string cancelUrl, 
        CancellationToken ct = default);

    /// <summary>取消 PayPal 订阅</summary>
    Task CancelSubscriptionAsync(
        string subscriptionId, 
        string reason, 
        CancellationToken ct = default);

    /// <summary>获取订阅详情</summary>
    Task<SubscriptionDetailResponse> GetSubscriptionDetailAsync(
        string subscriptionId, 
        CancellationToken ct = default);

    // ========== Webhook 验证 ==========
    
    /// <summary>验证 Webhook 签名</summary>
    Task<bool> VerifyWebhookSignatureAsync(
        string webhookId,
        IDictionary<string, string> headers, 
        string body, 
        CancellationToken ct = default);
}
```

## 使用示例

### 一次性付费流程

#### returnUrl 和 cancelUrl 说明

这两个参数是**你自己定义的前端页面地址**，传给 PayPal 用于控制用户授权后的跳转：

| 参数 | 触发条件 | 用途 |
|------|----------|------|
| `returnUrl` | 用户在 PayPal 页面**完成授权** | 跳转回你的支付成功/确认页面 |
| `cancelUrl` | 用户在 PayPal 页面**点击取消** | 跳转回你的支付取消页面 |

**注意：** 这两个 URL 必须是你的前端部署的实际可访问地址，不是 PayPal 提供的。

```csharp
public class PaymentService
{
    private readonly IPayPalClient _payPalClient;

    public PaymentService(IPayPalClient payPalClient)
    {
        _payPalClient = payPalClient;
    }

    // 步骤1：创建订单，返回 OrderId 和 ApproveUrl 给前端
    public async Task<CreateOrderResponse> CreatePaymentAsync(decimal amount)
    {
        var result = await _payPalClient.CreateOrderAsync(
            amount: amount,
            currency: "USD",
            // returnUrl: 用户在 PayPal 授权成功后跳转回这里（你的前端页面）
            returnUrl: "https://yoursite.com/payment/success",
            // cancelUrl: 用户在 PayPal 点击取消后跳转回这里（你的前端页面）
            cancelUrl: "https://yoursite.com/payment/cancel",
            description: "YiPix Lifetime License");

        // 返回完整响应，前端需要保存 OrderId 并重定向用户到 ApproveUrl
        // result.OrderId   - PayPal 订单 ID（后续捕获时需要）
        // result.ApproveUrl - 用户授权支付的 URL
        return result;
    }

    // 步骤2：用户在 PayPal 授权后回调，前端从 URL 参数获取 orderId 传给后端
    // PayPal 回调 URL 示例: https://yoursite.com/payment/success?token=XXXXX&PayerID=YYYYY
    // 其中 token 参数就是 OrderId
    public async Task<CaptureOrderResponse> CapturePaymentAsync(string orderId)
    {
        var result = await _payPalClient.CaptureOrderAsync(orderId);
        
        if (result.Status == "COMPLETED")
        {
            // 支付成功，更新数据库状态
            // result.CaptureId - 捕获 ID
            // result.Amount    - 实际扣款金额
        }
        
        return result;
    }
}

// ========== 前端流程说明 ==========
// 
// 1. 前端调用后端 API 创建订单:
//    POST /api/payment/create
//    响应: { "orderId": "5O190127TN364715T", "approveUrl": "https://www.paypal.com/..." }
//
// 2. 前端保存 orderId，然后重定向用户到 approveUrl（或在新窗口打开）
//
// 3. 用户在 PayPal 页面完成授权后，PayPal 重定向回 returnUrl:
//    https://yoursite.com/payment/success?token=5O190127TN364715T&PayerID=XXXXX
//    注意: URL 中的 token 参数就是 OrderId
//
// 4. 前端从 URL 解析 token（orderId），调用后端 API 确认支付:
//    POST /api/payment/capture
//    请求体: { "orderId": "5O190127TN364715T" }
//
// 5. 后端 Controller 收到请求，内部调用 _payPalClient.CaptureOrderAsync(orderId)
//    该方法会请求 PayPal API 完成扣款，并返回支付结果给前端
//
// 调用链路:
// 前端 POST /api/payment/capture 
//   → 后端 PaymentController.Capture()
//     → _payPalClient.CaptureOrderAsync(orderId) 
//       → PayPal API: POST v2/checkout/orders/{id}/capture
```

### 订阅流程

#### 订阅与一次性付费的关键区别

| 对比项 | 一次性付费 (Orders API) | 订阅 (Subscriptions API) |
|--------|-------------------------|--------------------------|
| 扣款时机 | 用户授权后需要手动 Capture | 用户授权后 **PayPal 自动扣款** |
| 后续扣款 | 无 | PayPal 按计划周期自动扣款 |
| 状态确认 | 通过 CaptureOrderAsync | 通过 **Webhook 事件** |
| Plan ID | 不需要 | 需要预先在 PayPal 后台创建 |

#### planId 说明

`planId` 是在 **PayPal 开发者后台预先创建的订阅计划 ID**，格式如 `P-XXXXXXXXXXXXXXXXXXXXXXXX`。

创建步骤：
1. 登录 [PayPal Developer Dashboard](https://developer.paypal.com/dashboard/)
2. 进入 Subscriptions → Plans
3. 创建 Product（产品）
4. 创建 Plan（计划），设置价格、周期（月/年）、试用期等
5. 获取 Plan ID，配置到 `appsettings.json` 的 `PlanIdMappings`

```csharp
public class SubscriptionService
{
    private readonly IPayPalClient _payPalClient;
    private readonly PayPalOptions _options;

    public SubscriptionService(IPayPalClient payPalClient, IOptions<PayPalOptions> options)
    {
        _payPalClient = payPalClient;
        _options = options.Value;
    }

    // 步骤1：创建订阅，返回 SubscriptionId 和 ApproveUrl 给前端
    public async Task<CreateSubscriptionResponse> CreateSubscriptionAsync(string planName)
    {
        // 从配置获取 PayPal Plan ID（如 "Monthly" → "P-XXXXX"）
        if (!_options.PlanIdMappings.TryGetValue(planName, out var planId))
            throw new ArgumentException($"Unknown plan: {planName}");

        var result = await _payPalClient.CreateSubscriptionAsync(
            planId: planId,
            // returnUrl: 用户授权成功后跳转（你的前端页面）
            returnUrl: "https://yoursite.com/subscription/success",
            // cancelUrl: 用户取消后跳转（你的前端页面）
            cancelUrl: "https://yoursite.com/subscription/cancel");

        // 返回完整响应：
        // result.SubscriptionId - PayPal 订阅 ID（需要保存到数据库）
        // result.Status         - 状态（通常为 APPROVAL_PENDING）
        // result.ApproveUrl     - 用户授权 URL
        return result;
    }

    // 步骤2：用户授权后，通过 Webhook 或主动查询确认订阅状态
    // 注意：订阅不需要像 Order 那样手动 Capture，PayPal 会自动扣款
    // 推荐通过 Webhook 事件 BILLING.SUBSCRIPTION.ACTIVATED 确认
    public async Task<SubscriptionDetailResponse> GetSubscriptionStatusAsync(string subscriptionId)
    {
        var result = await _payPalClient.GetSubscriptionDetailAsync(subscriptionId);
        
        // result.Status 可能的值：
        // - APPROVAL_PENDING: 等待用户授权
        // - APPROVED: 用户已授权（即将激活）
        // - ACTIVE: 订阅已激活（扣款成功）
        // - SUSPENDED: 订阅已暂停
        // - CANCELLED: 订阅已取消
        // - EXPIRED: 订阅已过期
        
        return result;
    }

    // 取消订阅
    public async Task CancelSubscriptionAsync(string subscriptionId, string reason = "User requested")
    {
        await _payPalClient.CancelSubscriptionAsync(subscriptionId, reason);
        // 取消后订阅状态变为 CANCELLED
        // 用户仍可使用服务直到当前周期结束（取决于你的业务逻辑）
    }
}

// ========== 前端流程说明 ==========
//
// 1. 前端调用后端 API 创建订阅:
//    POST /api/subscription/create
//    请求体: { "planName": "Monthly" }
//    响应: { "subscriptionId": "I-XXXXXX", "approveUrl": "https://www.paypal.com/..." }
//
// 2. 前端保存 subscriptionId，重定向用户到 approveUrl
//
// 3. 用户在 PayPal 页面登录并授权订阅
//
// 4. PayPal 重定向回 returnUrl:
//    https://yoursite.com/subscription/success?subscription_id=I-XXXXXX&ba_token=XXXXX
//    注意: URL 参数中有 subscription_id
//
// 5. 【重要区别】订阅不需要手动 Capture！
//    用户授权后，PayPal 会自动完成首次扣款
//    后端通过以下方式确认订阅激活：
//    - 方式A（推荐）: 监听 Webhook 事件 BILLING.SUBSCRIPTION.ACTIVATED
//    - 方式B: 前端轮询调用 GET /api/subscription/{id}/status 查询状态
//
// 6. 后续周期扣款由 PayPal 自动执行，通过 Webhook 通知：
//    - PAYMENT.SALE.COMPLETED: 周期扣款成功
//    - BILLING.SUBSCRIPTION.PAYMENT.FAILED: 扣款失败
//
// 调用链路（创建订阅）:
// 前端 POST /api/subscription/create
//   → 后端 SubscriptionController.Create()
//     → _payPalClient.CreateSubscriptionAsync(planId)
//       → PayPal API: POST v1/billing/subscriptions

### Webhook 处理

```csharp
[ApiController]
[Route("api/webhooks/paypal")]
public class PayPalWebhookController : ControllerBase
{
    private readonly IPayPalClient _payPalClient;
    private readonly PayPalOptions _options;

    public PayPalWebhookController(
        IPayPalClient payPalClient, 
        IOptions<PayPalOptions> options)
    {
        _payPalClient = payPalClient;
        _options = options.Value;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        // 读取原始请求体
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        // 提取签名相关的请求头
        var headers = new Dictionary<string, string>
        {
            ["PAYPAL-AUTH-ALGO"] = Request.Headers["PAYPAL-AUTH-ALGO"].ToString(),
            ["PAYPAL-CERT-URL"] = Request.Headers["PAYPAL-CERT-URL"].ToString(),
            ["PAYPAL-TRANSMISSION-ID"] = Request.Headers["PAYPAL-TRANSMISSION-ID"].ToString(),
            ["PAYPAL-TRANSMISSION-SIG"] = Request.Headers["PAYPAL-TRANSMISSION-SIG"].ToString(),
            ["PAYPAL-TRANSMISSION-TIME"] = Request.Headers["PAYPAL-TRANSMISSION-TIME"].ToString()
        };

        // 验证签名
        var isValid = await _payPalClient.VerifyWebhookSignatureAsync(
            _options.WebhookId, headers, body);

        if (!isValid)
            return Unauthorized("Invalid webhook signature");

        // 解析并处理事件
        var webhookEvent = JsonSerializer.Deserialize<JsonElement>(body);
        var eventType = webhookEvent.GetProperty("event_type").GetString();

        switch (eventType)
        {
            case "PAYMENT.CAPTURE.COMPLETED":
                // 处理支付完成
                break;
            case "BILLING.SUBSCRIPTION.ACTIVATED":
                // 处理订阅激活
                break;
            case "BILLING.SUBSCRIPTION.CANCELLED":
                // 处理订阅取消
                break;
        }

        return Ok();
    }
}
```

## 数据模型

### Orders API 模型

| 类名 | 说明 |
|------|------|
| `CreateOrderResponse` | 创建订单响应（OrderId, Status, ApproveUrl） |
| `CaptureOrderResponse` | 捕获订单响应（OrderId, Status, CaptureId, Amount, Currency） |
| `PayPalOrderRequest` | 创建订单请求体（内部使用） |
| `PayPalOrderResponse` | PayPal API 原始订单响应（内部使用） |
| `PayPalCaptureResponse` | PayPal API 原始捕获响应（内部使用） |

### Subscriptions API 模型

| 类名 | 说明 |
|------|------|
| `CreateSubscriptionResponse` | 创建订阅响应（SubscriptionId, Status, ApproveUrl） |
| `SubscriptionDetailResponse` | 订阅详情（SubscriptionId, Status, PlanId, StartTime, NextBillingTime） |
| `PayPalSubscriptionRequest` | 创建订阅请求体（内部使用） |
| `PayPalSubscriptionResponse` | PayPal API 原始订阅响应（内部使用） |

### Webhook 模型

| 类名 | 说明 |
|------|------|
| `WebhookVerifyRequest` | Webhook 签名验证请求体 |
| `WebhookVerifyResponse` | Webhook 签名验证响应 |

### 通用模型

| 类名 | 说明 |
|------|------|
| `PurchaseUnit` | 购买单元（金额、描述） |
| `AmountInfo` | 金额信息（货币、数值） |
| `LinkInfo` | HATEOAS 链接信息 |
| `PayPalTokenResponse` | OAuth2 Token 响应（内部使用） |

## 支付流程图

### 一次性付费流程

```
┌─────────┐     ┌─────────────┐     ┌─────────┐     ┌────────┐
│  前端   │     │  后端服务   │     │ PayPal  │     │  用户  │
└────┬────┘     └──────┬──────┘     └────┬────┘     └───┬────┘
     │                 │                 │              │
     │ 1. POST /api/payment/create      │              │
     │────────────────>│                 │              │
     │                 │                 │              │
     │                 │ 2. CreateOrderAsync            │
     │                 │────────────────>│              │
     │                 │                 │              │
     │                 │ 3. OrderId +    │              │
     │                 │    ApproveUrl   │              │
     │                 │<────────────────│              │
     │                 │                 │              │
     │ 4. 返回 { orderId, approveUrl }  │              │
     │<────────────────│                 │              │
     │                 │                 │              │
     │ 5. 前端保存 orderId              │              │
     │    重定向到 ApproveUrl ──────────────────────>  │
     │                 │                 │              │
     │                 │                 │ 6. 用户登录 │
     │                 │                 │    并授权   │
     │                 │                 │<─────────────│
     │                 │                 │              │
     │ 7. PayPal 重定向回 returnUrl     │              │
     │    ?token=OrderId&PayerID=xxx    │              │
     │<──────────────────────────────────│              │
     │                 │                 │              │
     │ 8. 前端从 URL 解析 token(orderId)│              │
     │    POST /api/payment/capture     │              │
     │    Body: { orderId }             │              │
     │────────────────>│                 │              │
     │                 │                 │              │
     │                 │ 9. CaptureOrderAsync(orderId) │
     │                 │────────────────>│              │
     │                 │                 │              │
     │                 │ 10. status: COMPLETED         │
     │                 │     captureId, amount         │
     │                 │<────────────────│              │
     │                 │                 │              │
     │ 11. 支付成功响应│                 │              │
     │<────────────────│                 │              │
     │                 │                 │              │
```

**关键点说明：**
- 步骤 4：后端返回 `orderId` 和 `approveUrl`，前端需要**保存 orderId**
- 步骤 7：PayPal 回调 URL 中的 `token` 参数就是 `orderId`
- 步骤 8：前端从 URL 参数解析 `token`，调用后端捕获接口完成扣款

### 订阅流程

```
┌─────────┐     ┌─────────────┐     ┌─────────┐     ┌────────┐
│  前端   │     │  后端服务   │     │ PayPal  │     │  用户  │
└────┬────┘     └──────┬──────┘     └────┬────┘     └───┬────┘
     │                 │                 │              │
     │ 1. POST /api/subscription/create │              │
     │    Body: { planName: "Monthly" } │              │
     │────────────────>│                 │              │
     │                 │                 │              │
     │                 │ 2. CreateSubscriptionAsync    │
     │                 │    (planId: P-XXXXX)          │
     │                 │────────────────>│              │
     │                 │                 │              │
     │                 │ 3. SubscriptionId +           │
     │                 │    ApproveUrl   │              │
     │                 │    status: APPROVAL_PENDING   │
     │                 │<────────────────│              │
     │                 │                 │              │
     │ 4. 返回 { subscriptionId, approveUrl }         │
     │    后端保存 subscriptionId 到数据库            │
     │<────────────────│                 │              │
     │                 │                 │              │
     │ 5. 前端重定向到 ApproveUrl ──────────────────> │
     │                 │                 │              │
     │                 │                 │ 6. 用户登录 │
     │                 │                 │    选择付款方式
     │                 │                 │    确认授权 │
     │                 │                 │<─────────────│
     │                 │                 │              │
     │ 7. PayPal 重定向回 returnUrl     │              │
     │    ?subscription_id=I-XXX&ba_token=XXX         │
     │<──────────────────────────────────│              │
     │                 │                 │              │
     │ 8. 前端显示"订阅处理中"         │              │
     │    (订阅不需要手动 Capture)      │              │
     │                 │                 │              │
     │                 │  ══════════════════════════   │
     │                 │  ║ PayPal 自动完成首次扣款 ║  │
     │                 │  ══════════════════════════   │
     │                 │                 │              │
     │                 │ 9. Webhook: BILLING.SUBSCRIPTION.ACTIVATED
     │                 │    { subscription_id, status: ACTIVE }
     │                 │<────────────────│              │
     │                 │                 │              │
     │                 │ 10. 更新数据库订阅状态为 ACTIVE
     │                 │     开通用户订阅权益          │
     │                 │                 │              │
     │ 11. 前端轮询或 WebSocket 通知订阅成功          │
     │<────────────────│                 │              │
     │                 │                 │              │
```

**后续周期自动扣款流程：**

```
┌─────────────┐              ┌─────────┐
│  后端服务   │              │ PayPal  │
└──────┬──────┘              └────┬────┘
       │                          │
       │   （每月/每年到期时）    │
       │                          │
       │  Webhook: PAYMENT.SALE.COMPLETED（扣款成功）
       │  或 BILLING.SUBSCRIPTION.PAYMENT.FAILED（扣款失败）
       │<─────────────────────────│
       │                          │
       │  更新订阅状态 / 发送通知 │
       │                          │
```

**关键点说明：**
- 步骤 8：**订阅不需要像 Order 那样调用 Capture**，PayPal 会自动扣款
- 步骤 9：通过 Webhook 事件确认订阅激活，这是最可靠的方式
- 后续扣款：无需你的系统触发，PayPal 按计划周期自动执行并通过 Webhook 通知

## 内部实现细节

### OAuth2 Token 缓存

`PayPalClient` 内部实现了 Token 缓存机制：

- 使用 `SemaphoreSlim` 保证线程安全
- Token 提前 60 秒刷新，避免竞态条件
- 自动处理 Token 过期和续期

```csharp
private string? _cachedToken;
private DateTime _tokenExpiresAt = DateTime.MinValue;
private readonly SemaphoreSlim _tokenLock = new(1, 1);

private async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
{
    await _tokenLock.WaitAsync(ct);
    try
    {
        // 缓存未过期则直接使用（提前 60 秒刷新）
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiresAt.AddSeconds(-60))
            return _cachedToken;
        
        // ... 获取新 Token
    }
    finally
    {
        _tokenLock.Release();
    }
}
```

### JSON 序列化

使用 `System.Text.Json` 进行 JSON 序列化，采用 snake_case 命名策略以符合 PayPal API 规范：

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true
};
```

### 错误处理

- API 错误会抛出 `HttpRequestException`，包含状态码和响应体
- 所有错误都会通过 `ILogger` 记录
- Webhook 验证失败返回 `false` 而非抛出异常

## 环境配置

### Sandbox（测试环境）

```json
{
  "PayPal": {
    "BaseUrl": "https://api-m.sandbox.paypal.com",
    "ClientId": "SANDBOX_CLIENT_ID",
    "ClientSecret": "SANDBOX_CLIENT_SECRET"
  }
}
```

### Live（生产环境）

```json
{
  "PayPal": {
    "BaseUrl": "https://api-m.paypal.com",
    "ClientId": "LIVE_CLIENT_ID",
    "ClientSecret": "LIVE_CLIENT_SECRET"
  }
}
```

## 常见 Webhook 事件类型

| 事件类型 | 说明 |
|----------|------|
| `PAYMENT.CAPTURE.COMPLETED` | 一次性支付完成 |
| `PAYMENT.CAPTURE.REFUNDED` | 支付已退款 |
| `BILLING.SUBSCRIPTION.CREATED` | 订阅已创建 |
| `BILLING.SUBSCRIPTION.ACTIVATED` | 订阅已激活 |
| `BILLING.SUBSCRIPTION.CANCELLED` | 订阅已取消 |
| `BILLING.SUBSCRIPTION.SUSPENDED` | 订阅已暂停 |
| `BILLING.SUBSCRIPTION.EXPIRED` | 订阅已过期 |
| `BILLING.SUBSCRIPTION.PAYMENT.FAILED` | 订阅扣款失败 |

## 注意事项

1. **敏感信息保护**：`ClientId` 和 `ClientSecret` 不应硬编码，建议使用环境变量或密钥管理服务。

2. **Plan ID 预配置**：订阅计划（Plan）需要在 PayPal 开发者后台预先创建，然后将 Plan ID 配置到 `PlanIdMappings`。

3. **Webhook URL 配置**：需要在 PayPal 开发者后台配置 Webhook URL，并获取 Webhook ID。

4. **货币格式**：金额使用 `decimal` 类型，序列化时格式化为两位小数（如 `"9.90"`）。

5. **幂等性**：`CaptureOrderAsync` 不是幂等的，同一订单多次调用会失败。

6. **超时处理**：HttpClient 使用默认超时，如需自定义可在注册时配置。

## 相关文档

- [PayPal 集成流程文档](PayPal%20集成流程文档.md)
- [PayPal 配置文档](PayPal%20配置文档.md)
- [订阅与支付流程文档](订阅与支付流程文档.md)
