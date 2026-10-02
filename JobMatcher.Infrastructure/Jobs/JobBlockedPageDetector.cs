namespace JobMatcher.Infrastructure.Jobs;

internal static class JobBlockedPageDetector
{
    public static BlockDetectionResult Detect(string? html, int? statusCode)
    {
        if (string.IsNullOrWhiteSpace(html))
            return statusCode is 403 or 429
                ? new BlockDetectionResult(true, 5, [$"http_{statusCode}"])
                : BlockDetectionResult.NotBlocked;

        var reasons = new List<string>();
        var score = 0;

        if (statusCode is 403 or 429)
        {
            score += 5;
            reasons.Add($"http_{statusCode}");
        }

        AddMarker("window.indeed_cloudflare_static_page", 5, "indeed_cloudflare_static_page");
        AddMarker("page_type:\"waf_block\"", 10, "indeed_waf_block");
        AddMarker("/cdn-cgi/challenge-platform/", 3, "cloudflare_challenge");
        AddMarker("request blocked", 3, "request_blocked");
        AddMarker("you have been blocked", 3, "you_have_been_blocked");
        AddMarker("access denied", 3, "access_denied");
        AddMarker("verify that you are human", 3, "human_verification");
        AddMarker("cf-chl-", 3, "cloudflare_challenge");

        return new BlockDetectionResult(score >= 5, score, reasons);

        void AddMarker(string marker, int markerScore, string reason)
        {
            if (html.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                score += markerScore;
                reasons.Add(reason);
            }
        }
    }

    public static bool IsBlocked(string? html, int? statusCode = null) =>
        Detect(html, statusCode).IsBlocked;

}

internal sealed record BlockDetectionResult(
    bool IsBlocked,
    int Score,
    IReadOnlyList<string> Reasons)
{
    public static readonly BlockDetectionResult NotBlocked =
        new(false, 0, Array.Empty<string>());
}
