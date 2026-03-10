# DownloadService CDN 签名集成指南

## 概述

本文档描述如何在 DownloadService 中接入 CDN 签名，实现安全的安装包下载链接分发。以**阿里云 CDN** 为主要示例，同时说明其他 CDN 供应商的差异。

## 整体流程

```
管理员上传安装包 → OSS/MinIO/S3 存储 → 配置 CDN 回源到存储桶
                                              ↓
用户请求下载 → DownloadService 生成签名 URL → 返回前端
                                              ↓
前端拿 URL → 直接从 CDN 节点下载 ← CDN 回源存储桶取文件
```

## 实施步骤

### 第 1 步：CDN 基础设施准备（运维侧）

以**阿里云 CDN** 为例，完整配置流程如下（其他云厂商步骤类似）：

#### 1.1 开通 CDN 服务

1. 登录 [阿里云控制台](https://cdn.console.aliyun.com)
2. 搜索并进入 **CDN** 产品页，点击 **开通服务**
3. 选择计费方式（推荐按流量计费，适合初期用量不大的场景）

#### 1.2 添加加速域名

1. 进入 CDN 控制台 → **域名管理** → **添加域名**
2. 填写加速域名，例如 `download.lilikk.com`
3. 业务类型选择 **大文件下载**（安装包属于大文件场景）
4. 加速区域根据用户分布选择（仅中国大陆 / 全球）
5. 点击 **下一步**，系统会生成一个 CNAME 地址，形如 `download.lilikk.com.w.kunlunaq.com`

#### 1.3 配置域名 CNAME 解析

到你的域名 DNS 解析商（如阿里云 DNS、Cloudflare 等）处添加记录：

| 记录类型 | 主机记录   | 记录值                                  | TTL  |
| -------- | ---------- | --------------------------------------- | ---- |
| CNAME    | download   | `download.lilikk.com.w.kunlunaq.com`   | 600  |

> **验证**：命令行执行 `nslookup download.lilikk.com`，返回结果包含 `kunlunaq.com` 即表示 CNAME 已生效。

#### 1.4 配置回源（指向存储源站）

根据你使用的存储服务选择对应方案：

##### 方案 A：阿里云 OSS（推荐，配置最简单）

阿里云 CDN + OSS 是同厂商组合，享有**原生集成**优势：

1. 添加加速域名时，**源站类型**直接选择 **OSS 域名**
2. 在下拉框中选择你的 OSS Bucket（如 `yipix-releases.oss-cn-shanghai.aliyuncs.com`）
3. 系统会自动填充回源地址和回源 Host，**无需手动配置**
4. **私有 Bucket 回源**：
   - 进入 CDN 控制台 → 域名管理 → 你的域名 → **回源配置**
   - 开启 **阿里云 OSS 私有 Bucket 回源**（一键开关）
   - CDN 会自动使用内部鉴权回源到 OSS，无需手动配置 Authorization 请求头
   - **推荐开启**，这样 OSS Bucket 保持私有，安全性由 CDN URL 鉴权保障

> **OSS 注意事项**：
> - OSS Bucket 的 **Region** 尽量与 CDN 回源节点就近，减少回源延迟
> - 如 Bucket 在 `oss-cn-shanghai`，CDN 加速区域包含中国大陆即可
> - OSS 内网回源不产生 OSS 外网流量费，**仅收 CDN 流量费**，更省钱

##### 方案 B：MinIO / AWS S3 / 其他 S3 兼容存储

1. 添加加速域名时，**源站类型**选择 **源站域名** 或 **IP**
2. **回源地址**：填你的存储服务地址
   - MinIO 部署在公网：`minio.yipix.com` 或公网 IP + 端口
   - AWS S3：`your-bucket.s3.ap-southeast-1.amazonaws.com`
3. **回源协议**：推荐选择 **HTTPS**（需确保源站支持 HTTPS）
4. **回源 Host**：填写存储桶域名，例如 `your-bucket.s3.amazonaws.com`
5. 如果是**私有存储桶**，需要在回源请求头中手动添加 `Authorization`（S3 V4 签名）

> **推荐做法（方案 B）**：将安装包存放在公开可读的存储桶中，安全性由 CDN 的 URL 鉴权来保障，这样回源配置最简单。

##### 两种方案对比

| 对比项         | 阿里云 OSS                | MinIO / S3              |
| -------------- | ------------------------- | ----------------------- |
| 回源配置       | 一键选择，自动填充        | 手动填写域名/IP         |
| 私有桶回源     | 一键开关，原生支持        | 需手动配签名头          |
| 回源流量费     | 内网回源免 OSS 外网流量费 | 正常收取源站出流量费    |
| 可靠性         | 同厂商链路，SLA 有保障    | 取决于源站网络质量      |
| 适用场景       | 已用阿里云，首选方案      | 自建存储或多云架构      |

#### 1.5 开启 HTTPS（强烈推荐）

1. 域名管理 → 点击你的域名 → **HTTPS 配置** → **修改配置**
2. 证书来源有三种方式：

| 方式             | 操作步骤                                                   | 适用场景           |
| ---------------- | ---------------------------------------------------------- | ------------------ |
| **CDN 免费证书** | 证书来源选「免费证书」→ 点确定，自动申请并部署，几分钟生效 | 最快最简单，首选   |
| **云盾已有证书** | 证书来源选「云盾证书」→ 下拉选择已申请的证书               | 已在 SSL 服务申请过 |
| **上传自有证书** | 证书来源选「自有证书」→ 上传 `.pem` + `.key` 文件          | Let's Encrypt 等   |

3. **强制 HTTPS 跳转**：开启，确保所有 HTTP 请求自动 301 到 HTTPS
4. **TLS 版本**：建议最低选择 TLS 1.2

> **免费证书说明**：阿里云免费 DV 证书有效期 3 个月，到期前 CDN 会自动续签。如需手动申请，参照下方流程。

#### 免费 SSL 证书手动申请与验证流程

如果在 CDN HTTPS 配置中选「免费证书」自动申请失败，或需要提前手动申请证书，按以下步骤操作：

**第一步：进入证书管理控制台**

1. 登录阿里云控制台，搜索 **数字证书管理服务**，进入控制台
2. 左侧菜单选择 **SSL 证书** → **免费证书**
3. 点击 **立即购买**，选择 **DV 单域名证书（免费试用）**
   - 每个阿里云账号每年可申请 20 张免费证书
   - 单张有效期 **3 个月**

**第二步：创建证书并填写信息**

1. 回到免费证书页面，点击 **创建证书**
2. 填写以下信息：
   - **域名**：填写要绑定的域名，如 `download.lilikk.com`
   - **验证方式**：选择 **DNS 验证**（推荐，域名在阿里云时可自动完成）
   - **联系人邮箱**：填写有效邮箱
3. 点击 **提交审核**

**第三步：DNS 验证**

提交后，阿里云会提供一条 TXT 验证记录，需要到 **域名 DNS 解析** 中添加。

1. 先确认你的域名 DNS 托管在哪里（阿里云万网、腾讯云、Cloudflare 等）：
   ```bash
   nslookup -type=NS lilikk.com
   ```
   - 返回 `*.hichina.com` → 阿里云万网
   - 返回 `*.dnspod.net` → 腾讯云 DNSPod
   - 返回 `*.cloudflare.com` → Cloudflare

2. 到**实际的 DNS 服务商控制台**，添加 TXT 解析记录：

   | 记录类型 | 主机记录 | 记录值 |
   |---------|---------|--------|
   | TXT | `_dnsauth.download` | 阿里云提供的验证值（一长串字符） |

   > **注意**：主机记录只填 `_dnsauth.download`，不要填完整域名（DNS 服务商会自动补上主域名后缀）。

3. 添加记录后等待 **1-2 分钟**，验证 DNS 是否生效：
   ```bash
   nslookup -type=TXT _dnsauth.download.lilikk.com
   ```
   - ✅ 返回了正确的验证值 → 生效，可以继续
   - ❌ 返回 `Non-existent domain` → 未生效，检查以下常见问题：
     - DNS 记录是否添加到了**正确的 DNS 服务商**（而非阿里云域名解析）
     - 记录类型是否选的 **TXT**（不是 CNAME 或 A）
     - 记录值前后是否有**多余空格或换行符**
     - 如果域名 DNS 不在阿里云，必须去实际托管平台添加

4. DNS 生效后，回到阿里云 **SSL 证书控制台**，点击 **验证**

**第四步：签发与部署**

1. 验证通过后，证书通常 **几分钟内** 自动签发
2. 签发成功后可以：
   - **直接在 CDN 中使用**：CDN 控制台 → HTTPS 配置 → 证书来源选「云盾证书」→ 下拉选择刚申请的证书
   - **下载到本地部署**：点击「下载」→ 选择 Nginx 格式 → 得到 `.pem`（证书）和 `.key`（私钥）文件

**常见问题排查**

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 验证超时 | DNS 记录未生效或添加位置不对 | 用 `nslookup -type=TXT` 确认记录是否存在 |
| `Non-existent domain` | DNS 服务商不对 | 用 `nslookup -type=NS` 确认 DNS 托管平台，到正确平台添加 |
| 记录已添加但查不到 | DNS 传播延迟 | 等待 5-10 分钟后重试 |
| 证书申请已过期 | 超过验证时限 | 撤销当前申请，重新创建证书（会生成新的验证值） |

**验证 HTTPS 生效**：
```bash
curl -I "https://download.lilikk.com"
# 返回 HTTP/2 或 HTTP/1.1 即表示 HTTPS 已生效
# 如果报 "SSL certificate problem" 说明证书还未部署完成，等几分钟再试
```

#### 1.6 配置 URL 鉴权

这是实现签名下载链接的**核心步骤**，未开启时 URL 鉴权页面会显示「未设置」。

**操作步骤**：

1. CDN 控制台 → **域名管理** → 点击你的域名（如 `download.lilikk.com`）
2. 左侧菜单 → **访问控制** → **URL 鉴权**
3. 点击 **修改配置**
4. 按以下表格填写：

| 配置项         | 填写内容                                             |
| -------------- | ---------------------------------------------------- |
| **鉴权功能**   | 开启                                                 |
| **鉴权类型**   | 类型 A（推荐）                                       |
| **主鉴权密钥** | 点击「随机生成」或手动输入 32+ 位密钥                |
| **备鉴权密钥** | 可选，点「随机生成」（用于密钥轮换时平滑过渡）       |
| **有效时长**   | 3600 秒（即 1 小时，与代码 `LinkExpirationMinutes` 一致）|

5. 点击 **确定** 保存

> **重要**：记下**主鉴权密钥**，后续需要配置到：
> - `appsettings.json` → `CdnSettings.AuthKey`
> - 或 `.env` → `CDN_AUTH_KEY`
> - 两边必须**完全一致**，否则签名校验失败

**类型 A 签名后的 URL 格式**：
```
https://download.lilikk.com/releases/v1.0.0/setup.exe?auth_key={timestamp}-{rand}-{uid}-{md5hash}
```

**鉴权效果**：
- 不带 `auth_key` 参数访问 → 返回 **403 Forbidden**
- 带正确签名访问 → 正常下载
- 签名过期后访问 → 返回 **403 Forbidden**

**开启后验证**：在 URL 鉴权页面下方有「**鉴权 URL 生成器**」，填入原始 URL（如 `https://download.lilikk.com/test.txt`），点击生成，用生成的 URL 在浏览器中测试能否正常访问。

**三种鉴权类型对比**：

| 类型   | URL 签名位置         | 适用场景     | 复杂度 |
| ------ | -------------------- | ------------ | ------ |
| 类型 A | Query 参数 auth_key  | 通用，最推荐 | 低     |
| 类型 B | URL 路径中嵌入时间戳 | 特殊场景     | 中     |
| 类型 C | URL 路径中嵌入 md5   | 特殊场景     | 中     |

#### 1.7 可选优化配置

| 配置项               | 推荐值        | 说明                                      |
| -------------------- | ------------- | ----------------------------------------- |
| 缓存过期时间         | 30 天         | 安装包内容不变，长期缓存可提高命中率      |
| 分片回源             | 开启          | 大文件加速，提高首次回源速度              |
| Range 回源           | 开启          | 支持断点续传下载                          |
| 智能压缩             | 关闭          | 安装包已压缩，重复压缩无意义              |
| Referer 防盗链       | 按需配置      | 可限制只允许你的前端域名发起下载          |
| IP 黑白名单          | 按需配置      | 可封禁异常 IP                             |

#### 1.8 验证 CDN 配置

完成以上步骤后，先手动验证 CDN 是否工作正常：

```bash
# 1. 上传一个测试文件到存储桶
#    阿里云 OSS：
ossutil cp test.txt oss://yipix-releases/test.txt
#    或者直接在 OSS 控制台上传
#
#    MinIO/S3：
#    mc cp test.txt myminio/yipix-releases/test.txt

# 2. 不带签名直接访问（应返回 403，说明鉴权生效）
curl -I "https://download.lilikk.com/test.txt"
# 期望: HTTP 403 Forbidden

# 3. 在阿里云控制台的「URL 鉴权」页面，使用「鉴权 URL 生成器」生成测试 URL
# 4. 用生成的 URL 访问（应返回 200，文件正常下载）
curl -I "https://download.lilikk.com/test.txt?auth_key=1741190400-0-0-abc123..."
# 期望: HTTP 200 OK
```

> 验证通过后，就可以进入第 2 步，在代码中对接签名服务了。

### 第 2 步：添加配置项

在 `appsettings.json` 中加入 CDN 配置：

```json
{
  "CdnSettings": {
    "BaseUrl": "https://download.lilikk.com",
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
https://download.lilikk.com/releases/v1.0.0/yipix-setup-windows.exe?auth_key=1741190400-0-0-a3f8b2c1e9d7...
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
