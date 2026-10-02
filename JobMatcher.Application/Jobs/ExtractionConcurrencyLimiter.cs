namespace JobMatcher.Application.Jobs;

public sealed class ExtractionConcurrencyLimiter(JobExtractionOptions options)
{
    private readonly SemaphoreSlim _semaphore = new(options.MaxConcurrentExtractions);

    public Task WaitAsync(CancellationToken cancellationToken) => _semaphore.WaitAsync(cancellationToken);

    public void Release() => _semaphore.Release();
}
