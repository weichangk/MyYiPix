# PayPal 配置问题修复记录

## 问题描述

### 问题一：项目中没有 PayPal 相关配置

**发现时间：** 2026-03-09

**现象：** PaymentService 的代码中大量引用 PayPal 相关字段（`PayPalOrderId`、`PayPalSubscriptionId`），但整个项目的配置文件中没有任何 PayPal 连接凭证：

| 配置文件 | 修复前状态 |
|---|---|
| `PaymentService/appsettings.json` | 仅有 Logging 和 AllowedHosts，无 PayPal 节 |
| `PaymentService/appsettings.Development.json` | 仅有 Logging，无 PayPal 节 |
| `docker/.env.example` | 仅有 PostgreSQL、RabbitMQ、JWT、CDN 配置，无 PayPal 变量 |

**影响：** 即使后续实现 PayPal API 调用逻辑，也无法获取 ClientId/ClientSecret 等必要凭证，无法连接 PayPal。

---

### 问题二：支付服务没有读取配置的代码

**现象：** 即使在配置文件中添加了 PayPal 配置节，代码中也没有任何地方读取它：

- `Program.cs` 没有 `Configure<T>()` 绑定
- `PaymentAppService` 没有注入任何配置对象
- 不存在 PayPal 配置类（Options Pattern）

**影响：** 配置文件中的值无法被代码使用，整条链路断裂。

---

## 修复方案

### 修复一：补充配置文件

**`src/Services/PaymentService/appsettings.json`**（生产环境模板，值留空通过环境变量注入）：

```json
{
  "PayPal": {
    "ClientId": "",
    "ClientSecret": "",
    "BaseUrl": "https://api-m.paypal.com",
    "WebhookId": ""
  }
}
```

**`src/Services/PaymentService/appsettings.Development.json`**（开发环境，指向 Sandbox）：

```json
{
  "PayPal": {
    "ClientId": "你的Sandbox_ClientId",
    "ClientSecret": "你的Sandbox_ClientSecret",
    "BaseUrl": "https://api-m.sandbox.paypal.com",
    "WebhookId": "你的Sandbox_WebhookId"
  }
}
```

**`docker/.env.example`**（Docker 部署环境变量模板）：

```dotenv
# PayPal
PAYPAL_CLIENT_ID=请替换为PayPal商家ClientId
PAYPAL_CLIENT_SECRET=请替换为PayPal商家ClientSecret
PAYPAL_BASE_URL=https://api-m.paypal.com
PAYPAL_WEBHOOK_ID=请替换为PayPal_WebhookId
```

### 修复二：新增配置类

新增文件 `src/Services/PaymentService/Application/PayPalOptions.cs`：

```csharp
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";
    public string WebhookId { get; set; } = string.Empty;
}
```

| 属性 | 用途 |
|---|---|
| `ClientId` | PayPal REST API 应用标识（绑定收款商家账号） |
| `ClientSecret` | PayPal REST API 密钥（与 ClientId 配对使用） |
| `BaseUrl` | API 地址（Sandbox: `api-m.sandbox.paypal.com`，Live: `api-m.paypal.com`） |
| `WebhookId` | Webhook 配置 ID（用于验证 Webhook 签名的真实性） |

### 修复三：DI 注册与注入

**`Program.cs`** 添加配置绑定：

```csharp
builder.Services.Configure<PayPalOptions>(
    builder.Configuration.GetSection(PayPalOptions.SectionName));
```

**`PaymentAppService`** 构造函数注入：

```csharp
public class PaymentAppService : IPaymentAppService
{
    private readonly IPaymentRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly PayPalOptions _paypalOptions;  // 新增

    public PaymentAppService(
        IPaymentRepository repository,
        IEventBus eventBus,
        IOptions<PayPalOptions> paypalOptions)      // 新增
    {
        _repository = repository;
        _eventBus = eventBus;
        _paypalOptions = paypalOptions.Value;        // 新增
    }
}
```

---

## 修复后的配置读取链路

```
appsettings.json / appsettings.Development.json
    │
    │  环境变量可覆盖（如 PayPal__ClientId=xxx）
    ↓
┌─────────────────────────────────────────────────┐
│  Program.cs                                     │
│  Configure<PayPalOptions>(Section "PayPal")     │
│  将 JSON 配置节绑定到 PayPalOptions 类            │
└──────────────────────┬──────────────────────────┘
                       │ DI 容器注册为 IOptions<PayPalOptions>
                       ↓
┌─────────────────────────────────────────────────┐
│  PaymentAppService                              │
│  构造函数注入 IOptions<PayPalOptions>             │
│  通过 _paypalOptions.ClientId 等属性访问          │
└──────────────────────┬──────────────────────────┘
                       │
                       ↓
          后续 PayPal API 调用使用这些凭证
          （CreateOrder / CaptureOrder / VerifyWebhook）
```

---

## 配置值获取方式

这四个配置值需要到 PayPal 开发者后台获取：

| 步骤 | 操作 |
|---|---|
| 1 | 登录 [developer.paypal.com](https://developer.paypal.com) |
| 2 | 进入 **Dashboard → Apps & Credentials** |
| 3 | 切换到 **Sandbox**（开发）或 **Live**（生产）标签 |
| 4 | 点击 **Create App**，获得 `ClientId` 和 `ClientSecret` |
| 5 | 在 App 详情页 → **Webhooks** 标签添加 Webhook URL |
| 6 | 配置 URL 为 `https://your-domain/api/payments/webhook` |
| 7 | 获得 `WebhookId` |

---

## 环境区分

| 环境 | BaseUrl | 凭证来源 | 说明 |
|---|---|---|---|
| 开发/测试 | `https://api-m.sandbox.paypal.com` | Sandbox App | 使用虚拟货币，不会真实扣款 |
| 生产 | `https://api-m.paypal.com` | Live App | 真实交易，需使用 Business 账号 |

---

## PayPal 账号类型要求

### 个人账号 vs 企业账号对比

| 对比项 | Personal（个人账号） | Business（企业账号） |
|---|---|---|
| 创建 REST API App | ✅ 可以 | ✅ 可以 |
| 接收一次性付款 | ✅ 可以 | ✅ 可以 |
| Subscriptions API（周期订阅） | ❌ **不支持** | ✅ 支持 |
| 每月收款限额 | 有额度上限 | 无限制 |
| 手续费 | 较高 | 相对较低（可协商） |
| Webhook 配置 | ✅ 可以 | ✅ 可以 |
| 多用户管理 | ❌ 不支持 | ✅ 支持 |

### 对 YiPix 项目的影响

YiPix 的核心场景是 **订阅制付费**（Monthly / Yearly / Lifetime），需要用到 PayPal Subscriptions API：

- **一次性付费**（`PaymentType = "OneTime"`）→ 个人账号 **可以**
- **订阅制付费**（`PaymentType = "Subscription"`）→ 个人账号 **不可以**，必须使用 Business 账号

### 结论

> **YiPix 项目必须使用 PayPal Business（企业）账号。**

### 如何升级

升级为 Business 账号是 **免费** 的，不需要注册公司，个体/自由职业者也能开通：

1. 登录 [paypal.com](https://www.paypal.com)
2. 进入 **Settings → Account Settings**
3. 选择 **Upgrade to Business Account**
4. 填写业务信息（个人信息即可）
5. 完成后即可在 [developer.paypal.com](https://developer.paypal.com) 创建 Live App

### 常见问题

| 问题 | 答案 |
|---|---|
| 能收到哪个币种？ | 由 Create Order 时的 `currency` 参数决定（如 USD、EUR），商家账号需支持该币种 |
| 能收到多个账号吗？ | 一套 ClientId/Secret 只对应一个商家账号；多个收款方需要多套凭证或使用 PayPal Partner/Marketplace 模式 |
| 手续费怎么算？ | PayPal 从每笔收款中扣除费率（通常 2.9% + $0.30），到账金额 = 支付金额 - 手续费 |
| 余额如何提现？ | 登录 PayPal 商家账号，Withdraw 到绑定的银行账户 |
| 国内银行能收款吗？ | 可以，PayPal 支持提现到国内银行卡（需绑定） |

---

## 涉及文件清单

| 文件 | 变更类型 |
|---|---|
| `src/Services/PaymentService/Application/PayPalOptions.cs` | **新增** - 配置类 |
| `src/Services/PaymentService/Application/PaymentAppService.cs` | **修改** - 注入 `IOptions<PayPalOptions>` |
| `src/Services/PaymentService/Program.cs` | **修改** - 添加 `Configure<PayPalOptions>()` |
| `src/Services/PaymentService/appsettings.json` | **修改** - 添加 PayPal 配置节 |
| `src/Services/PaymentService/appsettings.Development.json` | **修改** - 添加 Sandbox 配置 |
| `docker/.env.example` | **修改** - 添加 PayPal 环境变量模板 |
