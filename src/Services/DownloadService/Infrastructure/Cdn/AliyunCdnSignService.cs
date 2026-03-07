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
