using AngleSharp.Html.Parser;
using JobMatcher.Domain.Jobs;
using System.Text.Json;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class JsonLdJobParser
{
    public async Task<JobPosting?> ParseAsync(
        string html,
        Uri sourceUrl,
        CancellationToken cancellationToken = default)
    {
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, cancellationToken);

        var scripts = document.QuerySelectorAll(
            "script[type='application/ld+json']");

        foreach (var script in scripts)
        {
            if (string.IsNullOrWhiteSpace(script.TextContent))
                continue;

            using var json = JsonDocument.Parse(script.TextContent);

            var job = TryParseJobPosting(json.RootElement, sourceUrl);

            if (job is not null)
                return job;
        }

        return null;
    }

    private static JobPosting? TryParseJobPosting(
        JsonElement element,
        Uri sourceUrl)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray()
                .Select(item => TryParseJobPosting(item, sourceUrl))
                .OfType<JobPosting>()
                .FirstOrDefault();

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty("@graph", out var graph))
        {
            var job = TryParseJobPosting(graph, sourceUrl);

            if (job is not null)
                return job;
        }

        if (!element.TryGetProperty("@type", out var type) ||
            type.GetString() != "JobPosting")
            return null;

        var title = element.TryGetProperty("title", out var titleElement)
            ? titleElement.GetString()
            : null;

        var description = element.TryGetProperty("description", out var descriptionElement)
            ? descriptionElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(description))
            return null;

        return new JobPosting(
            title,
            null,
            null,
            description,
            sourceUrl);
    }

}