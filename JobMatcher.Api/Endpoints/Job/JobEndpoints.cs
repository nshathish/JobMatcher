using JobMatcher.Application.Jobs;
using JobMatcher.Domain.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace JobMatcher.Api.Endpoints.Job;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(
        this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs")
            .WithTags("Job Operations");

        jobs.MapPost(
            "/extract",
            async Task<Results<
                Ok<ExtractedJob>,
                BadRequest<ProblemDetails>,
                ProblemHttpResult>> (
                ExtractJobRequest? request,
                JobExtractionService service,
                HttpContext httpContext,
                ILogger<JobEndpointLogger> logger,
                CancellationToken cancellationToken) =>
            {
                if (request is null)
                    return TypedResults.BadRequest(
                        new ProblemDetails
                        {
                            Title = "Invalid job URL",
                            Detail = "A request body is required."
                        });

                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url))
                    return TypedResults.BadRequest(
                        new ProblemDetails
                        {
                            Title = "Invalid job URL",
                            Detail = "The request must contain a valid absolute URL."
                        });

                if (!JobUrlPolicy.TryValidate(url, out var validationError))
                    return TypedResults.BadRequest(
                        new ProblemDetails
                        {
                            Title = "Invalid job URL",
                            Detail = validationError
                        });

                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    ["ExtractionId"] = httpContext.TraceIdentifier,
                    ["Host"] = url.Host
                });
                logger.LogDebug("Job extraction request started");

                var extraction = await service.ExtractAsync(
                    url,
                    httpContext.TraceIdentifier,
                    cancellationToken);

                if (extraction.Job is not null)
                    return TypedResults.Ok(extraction.Job);

                var statusCode = extraction.FailureCategory switch
                {
                    ExtractionFailureCategory.Timeout => StatusCodes.Status504GatewayTimeout,
                    ExtractionFailureCategory.Blocked or
                    ExtractionFailureCategory.HttpError or
                    ExtractionFailureCategory.BrowserError => StatusCodes.Status502BadGateway,
                    _ => StatusCodes.Status422UnprocessableEntity
                };

                var title = extraction.FailureCategory switch
                {
                    ExtractionFailureCategory.Timeout => "Job extraction timed out",
                    ExtractionFailureCategory.Blocked => "Job source blocked extraction",
                    ExtractionFailureCategory.HttpError or ExtractionFailureCategory.BrowserError => "Job source could not be reached",
                    _ => "No recognizable job posting found"
                };

                return TypedResults.Problem(
                    statusCode: statusCode,
                    title: title,
                    extensions: new Dictionary<string, object?>
                    {
                        ["extractionId"] = httpContext.TraceIdentifier,
                        ["attempts"] = extraction.Attempts
                    });
            })
            .WithName("ExtractJobPosting")
            .WithSummary("Extract Job Posting")
            .WithDescription("Extracts structured job posting details from a public job URL.");

        return app;
    }
}
