using JobMatcher.Domain.Jobs;

namespace JobMatcher.Application.Jobs;

public enum ExtractionOutcome
{
    Success,
    NoMatch,
    Failed
}

public enum ExtractionFailureCategory
{
    Timeout,
    Cancelled,
    Blocked,
    HttpError,
    InvalidContent,
    BrowserError,
    ParserError,
    Unsupported
}

public sealed record ExtractorResult(
    ExtractedJob? Job,
    ExtractionOutcome Outcome,
    ExtractionFailureCategory? FailureCategory = null,
    string? ErrorCode = null,
    int? HttpStatusCode = null,
    long? ResponseBytes = null);

public sealed record ExtractionAttempt(
    string Extractor,
    ExtractionOutcome Outcome,
    ExtractionFailureCategory? FailureCategory,
    double ElapsedMs,
    int? HttpStatusCode,
    long? ResponseBytes,
    string? ErrorCode);

public sealed record ExtractionResult(
    ExtractedJob? Job,
    IReadOnlyList<ExtractionAttempt> Attempts,
    ExtractionFailureCategory? FailureCategory);
