namespace JobMatcher.Application.Jobs;

public sealed class ExtractionConcurrencyLimiter(JobExtractionOptions options)
{
    private readonly SemaphoreSlim semaphore = new(options.MaxConcurrentExtractions);

    public Task WaitAsync(CancellationToken cancellationToken) => semaphore.WaitAsync(cancellationToken);

    public void Release() => semaphore.Release();
}
