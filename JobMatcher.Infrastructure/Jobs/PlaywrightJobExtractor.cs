using JobMatcher.Application.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class PlaywrightJobExtractor(
    HtmlJobParser parser,
    IOptions<JobExtractionOptions> options,
    ExtractionConcurrencyLimiter concurrencyLimiter,
    JobExtractionMetrics metrics,
    PlaywrightBrowserManager browserManager,
    ILogger<PlaywrightJobExtractor> logger) : IJobExtractor
{
    public bool CanHandle(Uri url) => JobSourcePolicy.IsGenericWebSource(url);

    public async Task<ExtractorResult> ExtractAsync(Uri url, string extractionId, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.BrowserTimeoutSeconds));
        var acquired = false;
        var browserStarted = false;
        IPage? page = null;
        IBrowserContext? context = null;

        try
        {
            await concurrencyLimiter.WaitAsync(timeout.Token);
            acquired = true;

            metrics.BrowserStarted();
            browserStarted = true;

            var browser = await browserManager.GetBrowserAsync(timeout.Token);
            context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "en-GB",
                TimezoneId = "Europe/London",
                JavaScriptEnabled = true
            });
            page = await context.NewPageAsync();
            var milliseconds = options.Value.BrowserTimeoutSeconds * 1000;

            page.SetDefaultNavigationTimeout(milliseconds);
            page.SetDefaultTimeout(milliseconds);

            await page.RouteAsync("**/*", async route =>
            {
                var requestUri = new Uri(route.Request.Url);
                if (string.Equals(route.Request.ResourceType, "document", StringComparison.OrdinalIgnoreCase) &&
                    !JobUrlPolicy.IsAllowedRedirect(url, requestUri))
                {
                    logger.LogInformation(
                        "Document navigation rejected. OriginalHost={OriginalHost}, RedirectHost={RedirectHost}",
                        url.Host,
                        requestUri.Host);
                    await route.AbortAsync();
                    return;
                }

                await route.ContinueAsync();
            });

            var response = await page.GotoAsync(
                url.ToString(),
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = milliseconds
                }).WaitAsync(timeout.Token);

            if (response is null)
            {
                return new ExtractorResult(
                    null,
                    ExtractionOutcome.Failed,
                    ExtractionFailureCategory.BrowserError,
                    "navigation_no_response");
            }

            var finalHost = Uri.TryCreate(page.Url, UriKind.Absolute, out var finalUri)
                ? finalUri.Host
                : null;
            logger.LogInformation(
                "Browser navigation completed for {ExtractionId}. Status={Status}, RequestedHost={RequestedHost}, FinalHost={FinalHost}",
                extractionId,
                response.Status,
                url.Host,
                finalHost);

            await page.WaitForLoadStateAsync(
                LoadState.DOMContentLoaded,
                new PageWaitForLoadStateOptions { Timeout = milliseconds });

            try
            {
                await page.WaitForSelectorAsync(
                    "main, article, h1, [role='main']",
                    new PageWaitForSelectorOptions
                    {
                        State = WaitForSelectorState.Attached,
                        Timeout = Math.Min(milliseconds, 5_000)
                    }).WaitAsync(timeout.Token);
            }
            catch (TimeoutException)
            {
                // Some valid job pages expose content only in the document body.
                // Let the conservative HTML parser make the final determination.
            }

            var html = await page.ContentAsync().WaitAsync(timeout.Token);
            var blockDetection = JobBlockedPageDetector.Detect(html, response.Status);
            if (blockDetection.IsBlocked)
            {
                logger.LogWarning(
                    "Browser navigation blocked for {ExtractionId} {Host}. Status={Status}, Reason={Reason}",
                    extractionId,
                    url.Host,
                    response.Status,
                    blockDetection.Reasons[0]);

                return new ExtractorResult(
                    null,
                    ExtractionOutcome.Failed,
                    ExtractionFailureCategory.Blocked,
                    $"blocked_{blockDetection.Reasons[0]}",
                    response.Status);
            }

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
            if (page is not null)
                await page.CloseAsync();

            if (context is not null)
                await context.CloseAsync();

            if (browserStarted)
                metrics.BrowserFinished();

            if (acquired)
                concurrencyLimiter.Release();
        }
    }
}
