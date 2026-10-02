using JobMatcher.Application.Jobs;
using JobMatcher.Domain.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class PlaywrightJobExtractor(
    HtmlJobParser parser,
    IOptions<JobExtractionOptions> options,
    ExtractionConcurrencyLimiter concurrencyLimiter,
    JobExtractionMetrics metrics,
    ILogger<PlaywrightJobExtractor> logger) : IJobExtractor
{
    public bool CanHandle(Uri url) => JobSourcePolicy.IsGenericWebSource(url);

    public async Task<ExtractorResult> ExtractAsync(Uri url, string extractionId, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.BrowserTimeoutSeconds));
        await concurrencyLimiter.WaitAsync(timeout.Token);
        metrics.BrowserStarted();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            await using var page = await browser.NewPageAsync();
            var milliseconds = options.Value.BrowserTimeoutSeconds * 1000;

            page.SetDefaultNavigationTimeout(milliseconds);
            page.SetDefaultTimeout(milliseconds);
            using var cancellationRegistration = timeout.Token.Register(() => _ = page.CloseAsync());

            await page.RouteAsync("**/*", async route =>
            {
                var requestUri = new Uri(route.Request.Url);
                if (string.Equals(route.Request.ResourceType, "document", StringComparison.OrdinalIgnoreCase) &&
                    !JobUrlPolicy.IsAllowedRedirect(url, requestUri))
                {
                    await route.AbortAsync();
                    return;
                }

                await route.ContinueAsync();
            });

            await page.GotoAsync(url.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = milliseconds }).WaitAsync(timeout.Token);
            var html = await page.ContentAsync().WaitAsync(timeout.Token);
            if (JobBlockedPageDetector.IsBlocked(html))
                return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.Blocked, "blocked_page");

            var job = await parser.ParseAsync(html, url, timeout.Token);
            return job is null
                ? new ExtractorResult(null, ExtractionOutcome.NoMatch)
                : new ExtractorResult(job, ExtractionOutcome.Success);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Browser extraction timed out for {ExtractionId} {Host}", extractionId, url.Host);
            return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.Timeout, "timeout");
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(exception, "Browser navigation timed out for {ExtractionId} {Host}", extractionId, url.Host);
            return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.Timeout, "timeout");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Browser extraction failed for {ExtractionId} {Host}", extractionId, url.Host);
            return new ExtractorResult(null, ExtractionOutcome.Failed, ExtractionFailureCategory.BrowserError, "browser_error");
        }
        finally
        {
            metrics.BrowserFinished();
            concurrencyLimiter.Release();
        }
    }
}
