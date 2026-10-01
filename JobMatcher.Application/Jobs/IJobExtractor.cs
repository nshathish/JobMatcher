using JobMatcher.Domain.Jobs;

namespace JobMatcher.Application.Jobs;

public interface IJobExtractor
{
    bool CanHandle(Uri url);

    Task<JobPosting?> ExtractAsync(
        Uri url,
        CancellationToken cancellationToken = default);
}