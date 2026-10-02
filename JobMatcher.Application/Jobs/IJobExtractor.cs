using JobMatcher.Domain.Jobs;

namespace JobMatcher.Application.Jobs;

public interface IJobSourceAdapter
{
    bool CanHandle(Uri url);

    Task<ExtractorResult> ExtractAsync(
        Uri url,
        string extractionId,
        CancellationToken cancellationToken = default);
}

public interface IJobExtractor : IJobSourceAdapter
{
}
