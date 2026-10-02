namespace JobMatcher.Infrastructure.Jobs;

internal static class JobBlockedPageDetector
{
    public static bool IsBlocked(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        return Contains(html, "troubleshooting cloudflare errors")
            || (Contains(html, "you have been blocked") &&
                (Contains(html, "ray id") || Contains(html, "request blocked")));
    }

    private static bool Contains(string html, string value) =>
        html.Contains(value, StringComparison.OrdinalIgnoreCase);
}
