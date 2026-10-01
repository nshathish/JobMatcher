using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JobMatcher.Application.Jobs;

public sealed class JobExtractionMetrics : IDisposable
{
    private readonly Meter meter = new("JobMatcher.Extraction", "1.0.0");
    private readonly Counter<long> requests;
    private readonly Counter<long> successes;
    private readonly Counter<long> noJobs;
    private readonly Counter<long> failures;
    private readonly Counter<long> attempts;
    private readonly Histogram<double> duration;
    private readonly Histogram<long> responseBytes;
    private readonly UpDownCounter<long> activeExtractions;
    private readonly UpDownCounter<long> activeBrowsers;

    public JobExtractionMetrics()
    {
        requests = meter.CreateCounter<long>("job_extraction_requests_total");
        successes = meter.CreateCounter<long>("job_extraction_successes_total");
        noJobs = meter.CreateCounter<long>("job_extraction_no_job_total");
        failures = meter.CreateCounter<long>("job_extraction_failures_total");
        attempts = meter.CreateCounter<long>("job_extraction_attempts_total");
        duration = meter.CreateHistogram<double>("job_extraction_duration_ms");
        responseBytes = meter.CreateHistogram<long>("job_extraction_response_bytes");
        activeExtractions = meter.CreateUpDownCounter<long>("job_extraction_active");
        activeBrowsers = meter.CreateUpDownCounter<long>("job_extraction_active_browsers");
    }

    public void RequestStarted() { requests.Add(1); activeExtractions.Add(1); }
    public void RequestFinished() => activeExtractions.Add(-1);
    public void Success(string extractor) => successes.Add(1, new KeyValuePair<string, object?>("extractor", extractor));
    public void NoJob() => noJobs.Add(1);
    public void Failure(ExtractionFailureCategory category) => failures.Add(1, new KeyValuePair<string, object?>("category", category.ToString()));
    public void Attempt(string extractor, ExtractionOutcome outcome) => attempts.Add(1, new TagList { { "extractor", extractor }, { "outcome", outcome.ToString() } });
    public void Duration(string extractor, ExtractionOutcome outcome, double elapsedMs) => duration.Record(elapsedMs, new TagList { { "extractor", extractor }, { "outcome", outcome.ToString() } });
    public void ResponseBytes(long bytes) => responseBytes.Record(bytes);
    public void BrowserStarted() => activeBrowsers.Add(1);
    public void BrowserFinished() => activeBrowsers.Add(-1);
    public void Dispose() => meter.Dispose();
}
