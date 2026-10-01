using System.Net;

namespace JobMatcher.Application.Jobs;

public static class JobUrlPolicy
{
    public static bool TryValidate(Uri? url, out string error)
    {
        if (url is null)
        {
            error = "A job URL is required.";
            return false;
        }

        if (!url.IsWellFormedOriginalString() ||
            (!string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            error = "The job URL must use http or https.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(url.Host) || url.UserInfo.Length > 0)
        {
            error = "The job URL must contain a host and must not contain user-info credentials.";
            return false;
        }

        if (IPAddress.TryParse(url.Host, out var address) && IsLocalAddress(address))
        {
            error = "Local and link-local job URL addresses are not allowed.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool IsAllowedRedirect(Uri initialUrl, Uri redirectUrl) =>
        TryValidate(redirectUrl, out _) &&
        string.Equals(initialUrl.Scheme, redirectUrl.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(initialUrl.Host, redirectUrl.Host, StringComparison.OrdinalIgnoreCase) &&
        initialUrl.Port == redirectUrl.Port;

    private static bool IsLocalAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return IPAddress.IsLoopback(address) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254 ||
            bytes.Length == 4 && (bytes[0] == 10 ||
                bytes[0] == 127 ||
                bytes[0] == 192 && bytes[1] == 168 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31);
    }
}
