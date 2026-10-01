using System.Text;
using JobMatcher.Application.Jobs;
using JobMatcher.Domain.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class JsonLdJobExtractor(
    HttpClient httpClient,
    JsonLdJobParser parser,
    IOptions<JobExtractionOptions> options,
    JobExtractionMetrics metrics,
    ILogger<JsonLdJobExtractor> logger) : IJobExtractor
{
    public bool CanHandle(Uri url) => true;

    public async Task<ExtractorResult> ExtractAsync(Uri url, string extractionId, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.HttpTimeoutSeconds));

        try
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is >= 300 and < 400)
                return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.Blocked, "redirect_blocked", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var category = response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests
                    ? ExtractionFailureCategory.Blocked
                    : ExtractionFailureCategory.HttpError;
                return new ExtractorResult(null, ExtractionOutcome.Failed, category, "http_error", (int)response.StatusCode);
            }

            var maxBytes = options.Value.MaxResponseBytes;
            if (response.Content.Headers.ContentLength > maxBytes)
                return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.InvalidContent, "response_too_large", (int)response.StatusCode, response.Content.Headers.ContentLength);

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using var buffer = new MemoryStream();
            var bytes = new byte[81920];
            var total = 0L;
            int read;
            while ((read = await stream.ReadAsync(bytes, timeout.Token)) > 0)
            {
                total += read;
                if (total > maxBytes)
                {
                    metrics.ResponseBytes(total);
                    return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.InvalidContent, "response_too_large", (int)response.StatusCode, total);
                }

                await buffer.WriteAsync(bytes.AsMemory(0, read), timeout.Token);
            }

            metrics.ResponseBytes(total);
            var html = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
            var job = await parser.ParseAsync(html, url, timeout.Token);
            return job is null
                ? new ExtractorResult(null, ExtractionOutcome.NoMatch, ResponseBytes: total)
                : new ExtractorResult(job, ExtractionOutcome.Success, ResponseBytes: total);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("JSON-LD extraction timed out for {ExtractionId} {Host}", extractionId, url.Host);
            return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.Timeout, "timeout");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "JSON-LD HTTP request failed for {ExtractionId} {Host}", extractionId, url.Host);
            return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.HttpError, "http_error");
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException)
        {
            logger.LogWarning(exception, "JSON-LD response could not be parsed for {ExtractionId} {Host}", extractionId, url.Host);
            return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.ParserError, "parser_error");
        }
    }
}
