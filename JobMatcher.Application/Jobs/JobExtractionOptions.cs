using System.ComponentModel.DataAnnotations;

namespace JobMatcher.Application.Jobs;

public sealed class JobExtractionOptions
{
    [Range(1, 300)]
    public int TotalTimeoutSeconds { get; set; } = 45;

    [Range(1, 120)]
    public int HttpTimeoutSeconds { get; set; } = 15;

    [Range(1, 180)]
    public int BrowserTimeoutSeconds { get; set; } = 30;

    [Range(1024, 50_000_000)]
    public int MaxResponseBytes { get; set; } = 5_000_000;

    [Range(1, 100)]
    public int MaxConcurrentExtractions { get; set; } = 4;

    [Range(0, 5)]
    public int MaxRedirects { get; set; } = 0;
}
