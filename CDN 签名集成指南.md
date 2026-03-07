# DownloadService CDN 签名集成指南

## 概述

本文档描述如何在 DownloadService 中接入 CDN 签名，实现安全的安装包下载链接分发。以**阿里云 CDN** 为主要示例，同时说明其他 CDN 供应商的差异。

## 整体流程

```
管理员上传安装包 → MinIO/S3 存储 → 配置 CDN 回源到 S3
                                          ↓
用户请求下载 → DownloadService 生成签名 URL → 返回前端
                                          ↓
前端拿 URL → 直接从 CDN 节点下载 ← CDN 回源 S3 取文件
```

## 实施步骤

### 第 1 步：CDN 基础设施准备（运维侧）

1. 购买/开通 CDN 服务（如阿里云 CDN）
2. 配置 CDN 加速域名，例如 `cdn.yipix.com`
3. 设置**回源地址**指向你的 MinIO/S3 存储桶
4. 开启 **URL 鉴权**（类型 A/B/C 任选，推荐类型 A）
5. 拿到鉴权密钥（`AuthKey`），配置到服务中

### 第 2 步：添加配置项

在 `appsettings.json` 中加入 CDN 配置：

```json
{
  "CdnSettings": {
    "BaseUrl": "https://cdn.yipix.com",
    "AuthKey": "your-cdn-auth-secret-key",
    "LinkExpirationMinutes": 60
  }
}
```

> **安全提醒**：生产环境请通过环境变量注入 `AuthKey`，不要提交到代码仓库。

### 第 3 步：创建 CDN 签名服务

#### 3.1 定义接口

新建文件 `Infrastructure/Cdn/ICdnSignService.cs`：

```csharp
namespace YiPix.Services.Download.Infrastructure.Cdn;

public interface ICdnSignService
{
    /// <summary>
    /// 生成带鉴权签名的 CDN 下载链接
    /// </summary>
    /// <param name="resourcePath">资源在 CDN 上的路径，如 /releases/v1.0.0/setup.exe</param>
    /// <param name="expiry">链接有效期，为 null 时使用配置的默认值</param>
    CdnSignedUrl GenerateSignedUrl(string resourcePath, TimeSpan? expiry = null);
}

public record CdnSignedUrl(string Url, DateTime ExpiresAt);
```

#### 3.2 阿里云 CDN 实现（类型 A 鉴权）

新建文件 `Infrastructure/Cdn/AliyunCdnSignService.cs`：

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace YiPix.Services.Download.Infrastructure.Cdn;

public class CdnSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthKey { get; set; } = string.Empty;
    public int LinkExpirationMinutes { get; set; } = 60;
}

/// <summary>
/// 阿里云 CDN URL 鉴权（类型A）
/// 签名格式: http://cdn.example.com/path?auth_key={timestamp}-{rand}-{uid}-{md5hash}
/// md5hash = md5("{path}-{timestamp}-{rand}-{uid}-{key}")
/// </summary>
public class AliyunCdnSignService : ICdnSignService
{
    private readonly CdnSettings _settings;

    public AliyunCdnSignService(IOptions<CdnSettings> settings)
    {
        _settings = settings.Value;
    }

    public CdnSignedUrl GenerateSignedUrl(string resourcePath, TimeSpan? expiry = null)
    {
        var expiryTime = expiry ?? TimeSpan.FromMinutes(_settings.LinkExpirationMinutes);
        var expiresAt = DateTime.UtcNow.Add(expiryTime);
        var timestamp = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();

        // 确保路径以 / 开头
        if (!resourcePath.StartsWith('/'))
            resourcePath = "/" + resourcePath;

        var rand = "0";  // 可随机生成，增强安全性
        var uid = "0";   // 用户标识，一般为 0

        // md5("{path}-{timestamp}-{rand}-{uid}-{key}")
        var stringToSign = $"{resourcePath}-{timestamp}-{rand}-{uid}-{_settings.AuthKey}";
        var md5Hash = ComputeMd5(stringToSign);

        var authKey = $"{timestamp}-{rand}-{uid}-{md5Hash}";
        var signedUrl = $"{_settings.BaseUrl.TrimEnd('/')}{resourcePath}?auth_key={authKey}";

        return new CdnSignedUrl(signedUrl, expiresAt);
    }

    private static string ComputeMd5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
```

**签名原理说明**：

- `timestamp`：链接的过期时间戳（Unix 格式），CDN 节点会检查是否过期
- `rand`：随机数，防止链接被猜测
- `uid`：用户标识，通常填 0
- `md5hash`：将路径 + 时间戳 + 随机数 + 用户标识 + 密钥拼接后进行 MD5 哈希
- CDN 收到请求后，用同样的密钥和算法重新计算 MD5，与 URL 中的签名对比，一致则放行

### 第 4 步：注册服务

在 `Program.cs` 中注册配置和服务：

```csharp
// 添加 CDN 签名服务
builder.Services.Configure<CdnSettings>(builder.Configuration.GetSection("CdnSettings"));
builder.Services.AddSingleton<ICdnSignService, AliyunCdnSignService>();
```

### 第 5 步：修改 DownloadAppService

#### 5.1 注入 `ICdnSignService`

```csharp
public class DownloadAppService : IDownloadAppService
{
    private readonly IDownloadRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICdnSignService _cdnSignService;

    public DownloadAppService(
        IDownloadRepository repository,
        IEventBus eventBus,
        ICdnSignService cdnSignService)
    {
        _repository = repository;
        _eventBus = eventBus;
        _cdnSignService = cdnSignService;
    }
}
```

#### 5.2 替换占位签名逻辑

将 `GetDownloadLinkAsync` 方法中的占位代码：

```csharp
// 改造前（占位实现）
// TODO: Generate CDN signed URL
var signedUrl = $"{release.DownloadUrl}?token={Guid.NewGuid()}&expires={DateTime.UtcNow.AddHours(1).Ticks}";
return new DownloadLinkResponse(signedUrl, DateTime.UtcNow.AddHours(1));
```

替换为：

```csharp
// 改造后（CDN 签名）
var signed = _cdnSignService.GenerateSignedUrl(release.DownloadUrl);
return new DownloadLinkResponse(signed.Url, signed.ExpiresAt);
```

### 第 6 步：调整 SoftwareRelease.DownloadUrl 的存储内容

接入 CDN 后，`DownloadUrl` 字段应存储**资源在 CDN 上的路径**（而非完整 URL），例如：

```
/releases/v1.0.0/yipix-setup-windows.exe
```

签名服务会自动拼上 CDN 域名 + 签名参数，最终生成的完整 URL 形如：

```
https://cdn.yipix.com/releases/v1.0.0/yipix-setup-windows.exe?auth_key=1741190400-0-0-a3f8b2c1e9d7...
```

---

## 不同 CDN 供应商的签名差异

| CDN 供应商   | 签名方式            | 关键参数              | 备注                       |
| ------------ | ------------------- | --------------------- | -------------------------- |
| 阿里云       | MD5（类型 A/B/C）   | AuthKey 密钥          | 推荐类型 A                 |
| AWS CloudFront | RSA 签名          | 密钥对 ID + 私钥文件  | 需创建 CloudFront Key Pair |
| Cloudflare   | HMAC-SHA256 Token   | Token 密钥            | 使用 Signed URL Token      |
| 腾讯云       | MD5（类型 A/B/C/D） | SignKey 密钥          | 与阿里云类似               |

通过 `ICdnSignService` 接口抽象，**切换 CDN 供应商只需替换实现类**，业务代码无需改动。

### 示例：AWS CloudFront 实现

如果需要对接 CloudFront，新建 `CloudFrontCdnSignService.cs`，使用 RSA 私钥签名：

```csharp
public class CloudFrontCdnSignService : ICdnSignService
{
    // 使用 AWS SDK: AWSSDK.CloudFront
    // 调用 AmazonCloudFrontUrlSigner.GetCannedSignedURL(...)
    // 参考: https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/private-content-signed-urls.html
}
```

然后在 `Program.cs` 中替换注册即可：

```csharp
builder.Services.AddSingleton<ICdnSignService, CloudFrontCdnSignService>();
```

---

## 文件变更清单

| 操作 | 文件路径                                          | 说明                   |
| ---- | ------------------------------------------------- | ---------------------- |
| 新建 | `Infrastructure/Cdn/ICdnSignService.cs`           | CDN 签名接口 + DTO     |
| 新建 | `Infrastructure/Cdn/AliyunCdnSignService.cs`      | 阿里云签名实现         |
| 修改 | `Application/DownloadAppService.cs`               | 注入并调用签名服务     |
| 修改 | `Program.cs`                                      | 注册配置和服务         |
| 修改 | `appsettings.json`                                | 添加 CdnSettings 配置  |

---

## 验证方法

1. 配置好 CDN 和鉴权密钥
2. 发布一个版本，`DownloadUrl` 填 CDN 资源路径
3. 调用 `GET /api/downloads/link/{version}/{platform}`
4. 检查返回的 URL 是否包含 `auth_key` 参数
5. 在浏览器中打开该 URL，确认可以正常下载
6. 等链接过期后再次访问，确认返回 403 Forbidden
