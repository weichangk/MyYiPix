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
