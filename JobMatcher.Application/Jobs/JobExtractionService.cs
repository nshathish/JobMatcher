using JobMatcher.Domain.Jobs;

namespace JobMatcher.Application.Jobs;

public sealed class JobExtractionService(
    IEnumerable<IJobExtractor> jobExtractors)
{
    public async Task<ExtractedJob?> ExtractAsync(
        Uri url,
        CancellationToken cancellationToken = default)
    {
        foreach (var extractor in jobExtractors)
        {
            if (!extractor.CanHandle(url))
                continue;

            var job = await extractor.ExtractAsync(url, cancellationToken);

            if (job is not null)
                return job;
        }

        return null;
    }
}
