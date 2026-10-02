using JobMatcher.Application.Jobs;
using Microsoft.Extensions.Logging;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class IndeedJobSourceAdapter(
    ILogger<IndeedJobSourceAdapter> logger) : IJobSourceAdapter
{
    public bool CanHandle(Uri url) => JobSourcePolicy.IsIndeed(url);

    public Task<ExtractorResult> ExtractAsync(
        Uri url,
        string extractionId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Indeed extraction requires an authorized partner API integration for {ExtractionId} {Host}",
            extractionId,
            url.Host);

        return Task.FromResult(
            new ExtractorResult(
                null,
                ExtractionOutcome.Failed,
                ExtractionFailureCategory.Unsupported,
                "indeed_authorization_required"));
    }
}
