namespace JobMatcher.Domain.Jobs;

public sealed record JobPosting(
    string Title,
    string? Company,
    string? Location,
    string Description,
    Uri SourceUrl);