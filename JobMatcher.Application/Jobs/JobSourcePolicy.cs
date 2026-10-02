namespace JobMatcher.Application.Jobs;

public static class JobSourcePolicy
{
    public static bool IsIndeed(Uri url)
    {
        var host = url.Host.TrimEnd('.');
        return string.Equals(host, "indeed.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".indeed.com", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "indeed.co.uk", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".indeed.co.uk", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGenericWebSource(Uri url) => !IsIndeed(url);
}
