using AngleSharp.Html.Parser;
using JobMatcher.Domain.Jobs;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class HtmlJobParser
{
    public async Task<ExtractedJob?> ParseAsync(string html, Uri sourceUrl, CancellationToken cancellationToken = default)
    {
        var document = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);
        foreach (var element in document.QuerySelectorAll("script, style, nav, footer, header, noscript, iframe"))
            element.Remove();

        var title = document.QuerySelector("h1")?.TextContent.Trim() ?? document.Title?.Trim() ?? string.Empty;
        var body = document.QuerySelector("main, article")?.TextContent.Trim() ?? document.Body?.TextContent.Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            return null;

        var description = NormalizeWhitespace(body);
        return new ExtractedJob(
            new JobIdentity(title, null, null, sourceUrl),
            description,
            new JobLocation(null, null, null, null, null),
            new EmploymentDetails(null, null, null, null),
            new SeniorityDetails(null, null, null),
            Array.Empty<JobComponent>(),
            Array.Empty<JobComponent>(),
            Array.Empty<JobSkill>(),
            null,
            Array.Empty<JobBenefit>(),
            new JobDates(null, null, DateTimeOffset.UtcNow),
            new JobProvenance(
                "rendered-html",
                new Dictionary<string, string>
                {
                    ["title"] = "h1 or document.title",
                    ["description"] = "main, article, or document.body"
                },
                ["HTML fallback did not identify structured job components."],
                0.55m));
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
