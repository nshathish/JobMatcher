using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JobMatcher.Application.Jobs;

public sealed class JobExtractionMetrics : IDisposable
{
    private readonly Meter _meter = new("JobMatcher.Extraction", "1.0.0");
    private readonly Counter<long> _requests;
    private readonly Counter<long> _successes;
    private readonly Counter<long> _noJobs;
    private readonly Counter<long> _failures;
    private readonly Counter<long> _attempts;
    private readonly Histogram<double> _duration;
    private readonly Histogram<long> _responseBytes;
    private readonly UpDownCounter<long> _activeExtractions;
    private readonly UpDownCounter<long> _activeBrowsers;

    public JobExtractionMetrics()
    {
        _requests = _meter.CreateCounter<long>("job_extraction_requests_total");
        _successes = _meter.CreateCounter<long>("job_extraction_successes_total");
        _noJobs = _meter.CreateCounter<long>("job_extraction_no_job_total");
        _failures = _meter.CreateCounter<long>("job_extraction_failures_total");
        _attempts = _meter.CreateCounter<long>("job_extraction_attempts_total");
        _duration = _meter.CreateHistogram<double>("job_extraction_duration_ms");
        _responseBytes = _meter.CreateHistogram<long>("job_extraction_response_bytes");
        _activeExtractions = _meter.CreateUpDownCounter<long>("job_extraction_active");
        _activeBrowsers = _meter.CreateUpDownCounter<long>("job_extraction_active_browsers");
    }

    public void RequestStarted() { _requests.Add(1); _activeExtractions.Add(1); }
    public void RequestFinished() => _activeExtractions.Add(-1);
    public void Success(string extractor) => _successes.Add(1, new KeyValuePair<string, object?>("extractor", extractor));
    public void NoJob() => _noJobs.Add(1);
    public void Failure(ExtractionFailureCategory category) => _failures.Add(1, new KeyValuePair<string, object?>("category", category.ToString()));
    public void Attempt(string extractor, ExtractionOutcome outcome) => _attempts.Add(1, new TagList { { "extractor", extractor }, { "outcome", outcome.ToString() } });
    public void Duration(string extractor, ExtractionOutcome outcome, double elapsedMs) => _duration.Record(elapsedMs, new TagList { { "extractor", extractor }, { "outcome", outcome.ToString() } });
    public void ResponseBytes(long bytes) => _responseBytes.Record(bytes);
    public void BrowserStarted() => _activeBrowsers.Add(1);
    public void BrowserFinished() => _activeBrowsers.Add(-1);
    public void Dispose() => _meter.Dispose();
}
