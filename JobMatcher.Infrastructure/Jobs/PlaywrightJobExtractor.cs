using JobMatcher.Application.Jobs;
using JobMatcher.Domain.Jobs;
using Microsoft.Playwright;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class PlaywrightJobExtractor(HtmlJobParser parser) : IJobExtractor
{
    public bool CanHandle(Uri url) => true;

    public async Task<JobPosting?> ExtractAsync(
        Uri url,
        CancellationToken cancellationToken = default)
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = true
            });

        var page = await browser.NewPageAsync();

        await page.GotoAsync(
            url.ToString(),
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

        var html = await page.ContentAsync();

        return await parser.ParseAsync(
            html,
            url,
            cancellationToken);

    }
}