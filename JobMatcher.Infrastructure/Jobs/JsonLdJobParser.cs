using AngleSharp.Html.Parser;
using JobMatcher.Domain.Jobs;
using System.Globalization;
using System.Text.Json;

namespace JobMatcher.Infrastructure.Jobs;

public sealed class JsonLdJobParser
{
    public async Task<ExtractedJob?> ParseAsync(string html, Uri sourceUrl, CancellationToken cancellationToken = default)
    {
        var document = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);

        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            if (string.IsNullOrWhiteSpace(script.TextContent))
                continue;

            try
            {
                using var json = JsonDocument.Parse(script.TextContent);
                var job = TryParseJobPosting(json.RootElement, sourceUrl);
                if (job is not null)
                    return job;
            }
            catch (JsonException)
            {
                // Ignore malformed unrelated JSON-LD and continue with other scripts.
            }
        }

        return null;
    }

    private static ExtractedJob? TryParseJobPosting(JsonElement element, Uri sourceUrl)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var job = TryParseJobPosting(item, sourceUrl);
                if (job is not null)
                    return job;
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty("@graph", out var graph))
        {
            var job = TryParseJobPosting(graph, sourceUrl);
            if (job is not null)
                return job;
        }

        if (!IsJobPostingType(element))
            return null;

        var title = GetString(element, "title");
        var description = GetString(element, "description");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return null;

        var organization = GetObject(element, "hiringOrganization");
        var location = GetObject(element, "jobLocation") ?? GetFirstObject(element, "jobLocation");
        var address = location is null ? null : GetObject(location.Value, "address");
        var responsibilities = GetComponents(element, "responsibilities", "responsibility");
        var qualifications = GetComponents(element, "qualifications", "qualification");
        var experience = GetString(element, "experienceRequirements");

        return new ExtractedJob(
            new JobIdentity(title.Trim(), organization is null ? null : GetString(organization.Value, "name"), GetString(element, "identifier"), sourceUrl),
            description.Trim(),
            new JobLocation(
                address is null ? null : GetString(address.Value, "addressLocality"),
                address is null ? null : GetString(address.Value, "addressRegion"),
                address is null ? null : GetString(address.Value, "addressCountry"),
                location is null ? null : GetString(location.Value, "name"),
                GetString(element, "jobLocationType")),
            new EmploymentDetails(
                string.Join(", ", GetStrings(element, "employmentType")),
                GetString(element, "employmentUnit"),
                GetString(element, "workHours"),
                GetString(element, "workHours")),
            new SeniorityDetails(null, ParseYears(experience), experience),
            responsibilities,
            qualifications,
            GetStrings(element, "skills").Select(skill => new JobSkill(skill, Normalize(skill), null, "unknown", skill)).ToArray(),
            ParseCompensation(GetObject(element, "baseSalary")),
            GetStrings(element, "jobBenefits").Select(benefit => new JobBenefit(benefit, Normalize(benefit))).ToArray(),
            new JobDates(ParseDate(GetString(element, "datePosted")), ParseDate(GetString(element, "validThrough")), DateTimeOffset.UtcNow),
            new JobProvenance(
                "json-ld",
                new Dictionary<string, string>
                {
                    ["title"] = "JobPosting.title",
                    ["description"] = "JobPosting.description",
                    ["company"] = "JobPosting.hiringOrganization.name",
                    ["location"] = "JobPosting.jobLocation",
                    ["skills"] = "JobPosting.skills"
                },
                Array.Empty<string>(),
                0.95m));
    }

    private static bool IsJobPostingType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type))
            return false;

        return type.ValueKind == JsonValueKind.String
            ? string.Equals(type.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase)
            : type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String && string.Equals(item.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonElement? GetObject(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static JsonElement? GetFirstObject(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in value.EnumerateArray())
            if (item.ValueKind == JsonValueKind.Object)
                return item;

        return null;
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static IEnumerable<string> GetStrings(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return Array.Empty<string>();

        if (value.ValueKind == JsonValueKind.String)
            return new[] { value.GetString()! };

        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)
            : Array.Empty<string>();
    }

    private static IReadOnlyList<JobComponent> GetComponents(JsonElement element, string property, string category) =>
        GetStrings(element, property)
            .Select(text => new JobComponent(text.Trim(), category, "unknown", text))
            .Where(component => !string.IsNullOrWhiteSpace(component.Text))
            .ToArray();

    private static CompensationDetails? ParseCompensation(JsonElement? salary)
    {
        if (salary is null)
            return null;

        var value = GetObject(salary.Value, "value");
        var minimum = ParseDecimal(value is null ? null : GetString(value.Value, "minValue"));
        var maximum = ParseDecimal(value is null ? null : GetString(value.Value, "maxValue"));
        var exact = ParseDecimal(value is null ? null : GetString(value.Value, "value"));

        return new CompensationDetails(
            minimum ?? exact,
            maximum ?? exact,
            GetString(salary.Value, "currency") ?? (value is null ? null : GetString(value.Value, "currency")),
            value is null ? null : GetString(value.Value, "unitText"),
            null,
            null);
    }

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) ? result : null;

    private static decimal? ParseYears(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var number = new string(value.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
        return ParseDecimal(number);
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
