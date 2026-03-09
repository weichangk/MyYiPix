# YiPix PayPal 集成流程文档

## 1. 概述

YiPix 平台使用 PayPal 作为支付网关，支持两种支付模式：

| 模式 | 适用场景 | PayPal API | 项目中的标识 |
|---|---|---|---|
| **Orders API** | 一次性付费 | `/v2/checkout/orders` | `PaymentType = "OneTime"`，字段 `PayPalOrderId` |
| **Subscriptions API** | 周期性订阅 | `/v1/billing/subscriptions` | `PaymentType = "Subscription"`，字段 `PayPalSubscriptionId` |

---

## 2. 模式一：Orders API（一次性付费）

### 2.1 完整流程

```
┌────────┐       ┌──────────┐       ┌────────┐
│  前端    │       │ 后端服务   │      │ PayPal  │
└───┬────┘       └────┬─────┘       └───┬────┘
    │                  │                 │
    │ ① 用户点击购买     │                 │
    │─────────────────>│                 │
    │                  │                 │
    │                  │ ② POST /v2/checkout/orders
    │                  │  {amount, currency,
    │                  │   return_url, cancel_url}
    │                  │────────────────>│
    │                  │                 │
    │                  │  返回 order_id   │
    │                  │  + approve_url  │
    │                  │<────────────────│
    │                  │                 │
    │ ③ 重定向到 PayPal  │                 │
    │  approve_url      │                 │
    │──────────────────────────────────>│
    │                  │                 │
    │ ④ 用户在 PayPal 登录并确认付款      │
    │                  │                 │
    │ ⑤ PayPal 重定向回 return_url       │
    │  附带 order_id    │                 │
    │<──────────────────────────────────│
    │                  │                 │
    │ ⑥ 调用后端 capture │                 │
    │  {PayPalOrderId}  │                 │
    │─────────────────>│                 │
    │                  │                 │
    │                  │ ⑦ POST /v2/checkout/orders/{id}/capture
    │                  │────────────────>│
    │                  │                 │
    │                  │  返回完成状态     │
    │                  │<────────────────│
    │                  │                 │
    │  支付完成 ✅       │                 │
    │<─────────────────│                 │
```

### 2.2 步骤详解

| 步骤 | 角色 | 操作 | 说明 |
|---|---|---|---|
| ① | 前端 | 用户点击购买按钮 | 前端调用后端 `POST /api/payments` |
| ② | 后端 | 调用 PayPal Create Order API | 传入金额、货币、`return_url`、`cancel_url`，获得 `order_id` 和 `approve_url` |
| ③ | 前端 | 重定向用户到 PayPal | 将用户跳转到 `approve_url`（PayPal 的授权页面） |
| ④ | 用户 | 在 PayPal 页面登录并授权 | 此时**钱还没扣**，仅用户确认了支付意图 |
| ⑤ | PayPal | 重定向回商户网站 | 用户授权后 PayPal 将用户重定向回 `return_url`，附带 `order_id` |
| ⑥ | 前端 | 调用后端 Capture | 前端调用 `POST /api/payments/capture`，传入 `PayPalOrderId` |
| ⑦ | 后端 | 调用 PayPal Capture API | **此时才真正扣款**，确认收钱 |

### 2.3 关键要点

- **两步式支付**：Create Order 只是创建订单并获取用户授权，Capture 才是真正扣款
- **`return_url`**：用户在 PayPal 确认后重定向回的前端页面（如 `/payment/success`）
- **`cancel_url`**：用户在 PayPal 取消后重定向回的前端页面（如 `/payment/cancel`）
- **Capture 是后端行为**：前端拿到 `order_id` 后调用后端 API，由后端去调 PayPal Capture

### 2.4 项目对应代码

| PayPal 操作 | 项目 API | 代码位置 | 状态 |
|---|---|---|---|
| Create Order | `POST /api/payments` | `PaymentAppService.CreatePaymentAsync` | ⚠️ TODO: 调用 PayPal API |
| Capture Order | `POST /api/payments/capture` | `PaymentAppService.CapturePaymentAsync` | ⚠️ TODO: 调用 PayPal API |

---

## 3. 模式二：Subscriptions API（周期性订阅）

### 3.1 完整流程

```
┌────────┐       ┌──────────┐       ┌────────┐
│  前端    │       │ 后端服务   │      │ PayPal  │
└───┬────┘       └────┬─────┘       └───┬────┘
    │                  │                 │
    │                  │ ⓪ 预先创建 Plan  │
    │                  │  (Monthly $9.9 / │
    │                  │   Yearly $99)    │
    │                  │────────────────>│
    │                  │  plan_id        │
    │                  │<────────────────│
    │                  │                 │
    │ ① 用户选择订阅计划 │                 │
    │─────────────────>│                 │
    │                  │                 │
    │                  │ ② POST /v1/billing/subscriptions
    │                  │  {plan_id, return_url, cancel_url}
    │                  │────────────────>│
    │                  │                 │
    │                  │  subscription_id│
    │                  │  + approve_url  │
    │                  │<────────────────│
    │                  │                 │
    │ ③ 重定向到 PayPal  │                 │
    │──────────────────────────────────>│
    │                  │                 │
    │ ④ 用户确认订阅     │                 │
    │<──────────────────────────────────│
    │                  │                 │
    │ ⑤ 后端确认激活     │                 │
    │─────────────────>│                 │
    │                  │                 │
    │                  │   ⑥ PayPal 每月/每年自动扣款
    │                  │                 │
    │                  │   ⑦ Webhook 通知
    │                  │   (每次扣款/取消/暂停)
    │                  │<────────────────│
    │                  │                 │
```

### 3.2 步骤详解

| 步骤 | 角色 | 操作 | 说明 |
|---|---|---|---|
| ⓪ | 后端/管理员 | 在 PayPal 创建 Plan | 一次性操作，定义价格和计费周期（如月付 $9.9） |
| ① | 前端 | 用户选择订阅计划 | 前端展示可用计划，用户选择 Monthly/Yearly/Lifetime |
| ② | 后端 | 调用 PayPal Create Subscription API | 传入 `plan_id` 和回调 URL，获得 `subscription_id` |
| ③ | 前端 | 重定向用户到 PayPal | 用户在 PayPal 同意订阅 |
| ④ | PayPal | 重定向回商户网站 | 回到 `return_url` |
| ⑤ | 后端 | 确认并激活订阅 | 在本地数据库创建 Subscription 记录 |
| ⑥ | PayPal | 自动周期扣款 | 每月/每年到期后自动从用户 PayPal 账户扣款 |
| ⑦ | PayPal | Webhook 通知 | 每次扣款/取消/暂停都会推送事件给后端 |

### 3.3 与一次性付费的关键区别

| 对比项 | Orders API（一次性） | Subscriptions API（订阅） |
|---|---|---|
| 扣款次数 | 一次 | 周期性自动扣款 |
| Capture 步骤 | 需要（手动确认扣款） | 不需要（自动扣款） |
| 后续通知方式 | 无 | Webhook 异步通知 |
| Plan 前置配置 | 不需要 | 需要预先创建 Plan |
| 退订 | 不适用 | 需要调用 Cancel Subscription API |

### 3.4 项目对应代码

| PayPal 操作 | 项目字段/API | 状态 |
|---|---|---|
| plan_id | `Payment.PlanId` / `CreatePaymentRequest.PlanId` | 字段已定义 |
| subscription_id | `Payment.PayPalSubscriptionId` / `Subscription.PayPalSubscriptionId` | 字段已定义 |
| Create Subscription | — | ❌ 尚未实现 |
| Cancel Subscription | `SubscriptionAppService.CancelSubscriptionAsync` | ⚠️ 本地取消已实现，PayPal 侧取消未实现 |

---

## 4. Webhook 机制

### 4.1 什么是 Webhook

PayPal 在发生特定事件时，会主动向商户预先注册的 URL 发送 HTTP POST 请求（即"推"模式），通知后端处理。

### 4.2 Webhook 配置

需要在 PayPal 开发者后台 (developer.paypal.com) 配置：

1. 进入 **Dashboard → My Apps & Credentials**
2. 选择应用 → **Webhooks** 标签
3. 添加 Webhook URL：`https://your-domain.com/api/payments/webhook`
4. 勾选需要监听的事件类型

### 4.3 常见事件类型

| PayPal 事件 | 触发时机 | 项目应处理的操作 |
|---|---|---|
| `PAYMENT.CAPTURE.COMPLETED` | 一次性支付到账 | 标记 Payment 为 Completed |
| `PAYMENT.CAPTURE.DENIED` | 支付被拒绝 | 标记 Payment 为 Failed |
| `PAYMENT.CAPTURE.REFUNDED` | 支付被退款 | 标记 Payment 为 Refunded |
| `BILLING.SUBSCRIPTION.ACTIVATED` | 订阅激活 | 创建/激活 Subscription |
| `BILLING.SUBSCRIPTION.CANCELLED` | 订阅被取消 | 取消 Subscription |
| `BILLING.SUBSCRIPTION.SUSPENDED` | 订阅暂停（扣款失败） | 标记 Subscription 为 PastDue/Suspended |
| `BILLING.SUBSCRIPTION.EXPIRED` | 订阅到期 | 标记 Subscription 为 Expired |
| `PAYMENT.SALE.COMPLETED` | 订阅周期扣款成功 | 记录续费、延长有效期 |

### 4.4 Webhook 处理流程

```
┌────────┐       ┌──────────────────────────┐
│ PayPal  │       │     PaymentService        │
└───┬────┘       └────────────┬─────────────┘
    │                          │
    │  POST /api/payments/webhook
    │  Headers:                │
    │   PAYPAL-TRANSMISSION-ID │
    │   PAYPAL-TRANSMISSION-TIME
    │   PAYPAL-CERT-URL        │
    │   PAYPAL-AUTH-ALGO       │
    │   PAYPAL-TRANSMISSION-SIG
    │  Body: { event_type, resource, ... }
    │─────────────────────────>│
    │                          │
    │                          │ ① 验证签名（调用 PayPal Verify API）
    │                          │    ⚠️ 当前为 TODO
    │                          │
    │                          │ ② 幂等性检查
    │                          │    查询 WebhookLog 表
    │                          │    (EventType + ResourceId) 是否已处理
    │                          │    → 已处理则直接跳过
    │                          │
    │                          │ ③ 根据 event_type 分发处理
    │                          │    → PAYMENT.CAPTURE.COMPLETED
    │                          │    → BILLING.SUBSCRIPTION.ACTIVATED
    │                          │    → BILLING.SUBSCRIPTION.CANCELLED
    │                          │    ⚠️ 各分支具体逻辑为 TODO
    │                          │
    │                          │ ④ 记录 WebhookLog
    │                          │    (EventType, ResourceId, Payload,
    │                          │     Processed, ProcessedAt)
    │                          │
    │         200 OK           │
    │<─────────────────────────│
```

### 4.5 签名验证（安全性）

每个 Webhook 请求头携带签名信息，后端应调用 PayPal 的 Verify Webhook Signature API 验证：

```
POST https://api-m.paypal.com/v1/notifications/verify-webhook-signature

{
  "auth_algo": "<PAYPAL-AUTH-ALGO>",
  "cert_url": "<PAYPAL-CERT-URL>",
  "transmission_id": "<PAYPAL-TRANSMISSION-ID>",
  "transmission_sig": "<PAYPAL-TRANSMISSION-SIG>",
  "transmission_time": "<PAYPAL-TRANSMISSION-TIME>",
  "webhook_id": "<YOUR-WEBHOOK-ID>",
  "webhook_event": <REQUEST-BODY>
}
```

返回 `verification_status: "SUCCESS"` 表示合法。**未验证签名就处理 Webhook 会有伪造回调的安全风险。**

### 4.6 幂等性保障

PayPal 可能因网络问题重复发送同一 Webhook。项目通过 `WebhookLog` 表的 `(EventType, ResourceId)` 联合索引实现幂等：

```csharp
// 已处理过的事件直接跳过
if (await _repository.WebhookAlreadyProcessedAsync(eventType, resourceId, ct))
    return;
```

---

## 5. PayPal API 认证

### 5.1 获取 Access Token

所有 PayPal API 调用前需要先获取 OAuth 2.0 Token：

```
POST https://api-m.paypal.com/v1/oauth2/token
Authorization: Basic <Base64(ClientId:Secret)>
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
```

返回：

```json
{
  "access_token": "A21AAF...",
  "token_type": "Bearer",
  "expires_in": 32400
}
```

### 5.2 环境区分

| 环境 | API Base URL | 用途 |
|---|---|---|
| Sandbox（沙箱） | `https://api-m.sandbox.paypal.com` | 开发测试 |
| Live（生产） | `https://api-m.paypal.com` | 正式环境 |

### 5.3 项目配置建议

在 `appsettings.json` 中添加：

```json
{
  "PayPal": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "BaseUrl": "https://api-m.sandbox.paypal.com",
    "WebhookId": "YOUR_WEBHOOK_ID"
  }
}
```

生产环境通过环境变量覆盖敏感信息。

---

## 6. 项目实现状态总览

### 6.1 已完成

| 功能 | 说明 |
|---|---|
| ✅ Payment 实体与数据库 | 支持 `PayPalOrderId`、`PayPalSubscriptionId`、状态流转 |
| ✅ Subscription 实体与数据库 | 支持多种计划和状态、变更历史记录 |
| ✅ Webhook 幂等机制 | `WebhookLog` 表 + `(EventType, ResourceId)` 去重 |
| ✅ 事件总线集成 | `PaymentCompletedEvent` 发布 → `WebhookWorker` 消费 |
| ✅ API 端点定义 | 创建支付、捕获支付、Webhook 接收入口 |
| ✅ DTO 与 Contract 定义 | `CreatePaymentRequest`（含 `ReturnUrl`、`CancelUrl`）等 |

### 6.2 待实现（TODO）

| 优先级 | 功能 | 对应代码位置 |
|---|---|---|
| 🔴 P0 | 调用 PayPal Create Order API | `PaymentAppService.CreatePaymentAsync` |
| 🔴 P0 | 调用 PayPal Capture Order API | `PaymentAppService.CapturePaymentAsync` |
| 🔴 P0 | 验证 Webhook 签名 | `PaymentsController.Webhook` |
| 🔴 P0 | 正确解析 Webhook JSON（当前硬编码） | `PaymentsController.Webhook` |
| 🔴 P0 | 支付完成后激活订阅 | `WebhookWorker.PaymentCompletedEventHandler` |
| 🟡 P1 | 调用 PayPal Create Subscription API | 尚无代码 |
| 🟡 P1 | 调用 PayPal Cancel Subscription API | `SubscriptionAppService.CancelSubscriptionAsync` |
| 🟡 P1 | Webhook 各事件类型的具体业务逻辑 | `PaymentAppService.ProcessWebhookAsync` |
| 🟢 P2 | PayPal Token 缓存与刷新 | 尚无代码 |

---

## 7. 推荐实现方案

### 7.1 引入 PayPal SDK 或 HttpClient 封装

建议在 `BuildingBlocks` 中新增 `PayPal` 模块：

```
BuildingBlocks/
└── PayPal/
    ├── IPayPalClient.cs           # 接口定义
    ├── PayPalClient.cs            # HttpClient 封装
    ├── PayPalOptions.cs           # 配置类
    └── Models/
        ├── CreateOrderRequest.cs
        ├── CreateOrderResponse.cs
        ├── CaptureOrderResponse.cs
        └── WebhookVerifyRequest.cs
```

### 7.2 核心接口设计

```csharp
public interface IPayPalClient
{
    // Orders API
    Task<CreateOrderResponse> CreateOrderAsync(decimal amount, string currency,
        string returnUrl, string cancelUrl);
    Task<CaptureOrderResponse> CaptureOrderAsync(string orderId);

    // Subscriptions API
    Task<CreateSubscriptionResponse> CreateSubscriptionAsync(string planId,
        string returnUrl, string cancelUrl);
    Task CancelSubscriptionAsync(string subscriptionId, string reason);

    // Webhook
    Task<bool> VerifyWebhookSignatureAsync(string webhookId,
        IDictionary<string, string> headers, string body);
}
```

### 7.3 前端集成方式

推荐使用 **PayPal JavaScript SDK**，可在前端直接渲染支付按钮，无需重定向：

```html
<script src="https://www.paypal.com/sdk/js?client-id=YOUR_CLIENT_ID"></script>
<div id="paypal-button-container"></div>
<script>
  paypal.Buttons({
    createOrder: () => fetch('/api/payments', { method: 'POST', body: ... })
      .then(res => res.json())
      .then(data => data.data.payPalOrderId),
    onApprove: (data) => fetch('/api/payments/capture', {
      method: 'POST',
      body: JSON.stringify({ payPalOrderId: data.orderID })
    })
  }).render('#paypal-button-container');
</script>
```

这种方式用户无需离开页面，体验更好。
