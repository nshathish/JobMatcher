namespace JobMatcher.Application.Jobs;

public interface IJobExtractor
{
    bool CanHandle(Uri url);

    Task<ExtractorResult> ExtractAsync(
        Uri url,
        string extractionId,
        CancellationToken cancellationToken = default);
}
