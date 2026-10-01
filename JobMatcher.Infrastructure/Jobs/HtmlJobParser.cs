using AngleSharp.Html.Parser;
using JobMatcher.Domain.Jobs;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class HtmlJobParser
{
    public async Task<JobPosting?> ParseAsync(
        string html,
        Uri sourceUrl,
        CancellationToken cancellationToken = default)
    {
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, cancellationToken);

        var title =
            document.QuerySelector("h1")?.TextContent.Trim()
            ?? document.Title;

        var body = document.Body?.TextContent.Trim();

        if (string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(body))
            return null;

        return new JobPosting(
            Title: title,
            Company: null,
            Location: null,
            Description: body,
            SourceUrl: sourceUrl);
    }
}