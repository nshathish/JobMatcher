using Microsoft.Playwright;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class PlaywrightBrowserManager : IAsyncDisposable
{
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken = default)
    {
        if (_browser is not null)
            return _browser;

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_browser is not null) return _browser;

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true });

            return _browser;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();

        _playwright?.Dispose();
        _initializationGate.Dispose();
    }
}
