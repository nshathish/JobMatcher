namespace JobMatcher.Domain.Jobs;

public sealed record ExtractedJob(
    JobIdentity Identity,
    string Description,
    JobLocation Location,
    EmploymentDetails Employment,
    SeniorityDetails Seniority,
    IReadOnlyList<JobComponent> Responsibilities,
    IReadOnlyList<JobComponent> Qualifications,
    IReadOnlyList<JobSkill> Skills,
    CompensationDetails? Compensation,
    IReadOnlyList<JobBenefit> Benefits,
    JobDates Dates,
    JobProvenance Provenance);

public sealed record JobIdentity(
    string Title,
    string? Company,
    string? ExternalId,
    Uri SourceUrl);

public sealed record JobLocation(
    string? City,
    string? Region,
    string? Country,
    string? DisplayName,
    string? RemoteMode);

public sealed record EmploymentDetails(
    string? Type,
    string? Duration,
    string? Schedule,
    string? WorkHours);

public sealed record SeniorityDetails(
    string? Level,
    decimal? MinimumYearsExperience,
    string? Requirements);

public sealed record JobComponent(
    string Text,
    string? Category,
    string Requiredness,
    string? Evidence);

public sealed record JobSkill(
    string Name,
    string? NormalizedName,
    string? Category,
    string Requiredness,
    string? Evidence);

public sealed record CompensationDetails(
    decimal? Minimum,
    decimal? Maximum,
    string? Currency,
    string? Period,
    string? Bonus,
    string? Equity);

public sealed record JobBenefit(
    string Text,
    string? NormalizedName);

public sealed record JobDates(
    DateTimeOffset? Posted,
    DateTimeOffset? Closing,
    DateTimeOffset Extracted);

public sealed record JobProvenance(
    string Parser,
    IReadOnlyDictionary<string, string> FieldsBySource,
    IReadOnlyList<string> Warnings,
    decimal Confidence);
