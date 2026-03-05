using System.Net;
using System.Net.Mail;
using System.Net.Sockets;

namespace YiPix.Services.Auth.Application;

public static class EmailValidator
{
    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "tempmail.com", "throwaway.email",
        "yopmail.com", "sharklasers.com", "guerrillamailblock.com", "grr.la",
        "dispostable.com", "trashmail.com", "fakeinbox.com", "maildrop.cc",
        "10minutemail.com", "temp-mail.org", "getnada.com"
    };

    /// <summary>
    /// 验证邮箱有效性：格式检查 + 一次性邮箱过滤 + DNS MX 记录验证
    /// </summary>
    public static async Task<(bool IsValid, string? ErrorMessage)> ValidateAsync(string email)
    {
        // 1. 格式验证
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email address is required.");

        try
        {
            var addr = new MailAddress(email);
            if (addr.Address != email.Trim())
                return (false, "Invalid email format.");
        }
        catch
        {
            return (false, "Invalid email format.");
        }

        var domain = email.Split('@')[1];

        // 2. 一次性邮箱过滤
        if (DisposableDomains.Contains(domain))
            return (false, "Disposable email addresses are not allowed.");

        // 3. DNS MX 记录验证（检查域名是否能接收邮件）
        try
        {
            var mxRecords = await Dns.GetHostEntryAsync(domain);
            if (mxRecords.AddressList.Length == 0)
                return (false, "Email domain does not exist.");
        }
        catch (SocketException)
        {
            return (false, "Email domain does not exist.");
        }

        return (true, null);
    }
}
