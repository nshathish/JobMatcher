using JobMatcher.Application.Jobs;
using JobMatcher.Domain.Jobs;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class JsonLdJobExtractor(
    HttpClient httpClient,
    JsonLdJobParser parser) : IJobExtractor
{
    public bool CanHandle(Uri url) => true;

    public async Task<ExtractedJob?> ExtractAsync(Uri url, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return await parser.ParseAsync(html, url, cancellationToken);
    }
}
