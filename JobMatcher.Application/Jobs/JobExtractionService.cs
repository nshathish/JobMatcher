using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobMatcher.Application.Jobs;

public sealed class JobExtractionService(
    IEnumerable<IJobSourceAdapter> jobSourceAdapters,
    IOptions<JobExtractionOptions> options,
    JobExtractionMetrics metrics,
    ILogger<JobExtractionService> logger)
{
    public async Task<ExtractionResult> ExtractAsync(Uri url, string extractionId, CancellationToken cancellationToken = default)
    {
        var attempts = new List<ExtractionAttempt>();
        var settings = options.Value;
        metrics.RequestStarted();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TotalTimeoutSeconds));

        try
        {
            foreach (var extractor in jobSourceAdapters)
            {
                if (!extractor.CanHandle(url))
                    continue;

                var extractorName = extractor.GetType().Name;
                var stopwatch = Stopwatch.StartNew();
                ExtractorResult result;

                try
                {
                    result = await extractor.ExtractAsync(url, extractionId, timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    result = new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.Timeout, "timeout");
                }
                catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
                {
                    result = new ExtractorResult(null, ExtractionOutcome.Failed, Classify(exception), "extractor_error");
                }

                stopwatch.Stop();
                var attempt = new ExtractionAttempt(
                    extractorName,
                    result.Outcome,
                    result.FailureCategory,
                    stopwatch.Elapsed.TotalMilliseconds,
                    result.HttpStatusCode,
                    result.ResponseBytes,
                    result.ErrorCode);
                attempts.Add(attempt);
                metrics.Attempt(extractorName, result.Outcome);
                metrics.Duration(extractorName, result.Outcome, attempt.ElapsedMs);

                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    ["ExtractionId"] = extractionId,
                    ["Extractor"] = extractorName,
                    ["Host"] = url.Host,
                    ["Outcome"] = result.Outcome.ToString(),
                    ["FailureCategory"] = result.FailureCategory?.ToString(),
                    ["ElapsedMs"] = attempt.ElapsedMs,
                    ["HttpStatusCode"] = result.HttpStatusCode,
                    ["ResponseBytes"] = result.ResponseBytes
                });

                if (result.Job is not null)
                {
                    metrics.Success(extractorName);
                    logger.LogDebug("Job extraction succeeded");
                    return new ExtractionResult(result.Job, attempts, null);
                }

                if (result.FailureCategory is not null)
                {
                    metrics.Failure(result.FailureCategory.Value);
                    logger.LogWarning("Job extractor failed with category {FailureCategory}", result.FailureCategory);
                }
                else
                {
                    logger.LogDebug("Job extractor found no recognizable job");
                }
            }

            metrics.NoJob();
            var finalFailure = attempts.LastOrDefault(attempt => attempt.FailureCategory is not null)?.FailureCategory;
            return new ExtractionResult(null, attempts, finalFailure);
        }
        finally
        {
            metrics.RequestFinished();
        }
    }

    private static ExtractionFailureCategory Classify(Exception exception) => exception switch
    {
        HttpRequestException => ExtractionFailureCategory.HttpError,
        _ => ExtractionFailureCategory.ParserError
    };
}
