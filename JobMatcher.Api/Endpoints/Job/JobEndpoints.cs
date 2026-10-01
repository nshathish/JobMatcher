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
        app.MapPost(
            "/api/jobs/extract",
            async Task<Results<
                Ok<JobPosting>,
                BadRequest<ProblemDetails>,
                NotFound<ProblemDetails>>> (
                ExtractJobRequest request,
                JobExtractionService service,
                CancellationToken cancellationToken) =>
            {
                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url))
                    return TypedResults.BadRequest(
                        new ProblemDetails
                        {
                            Title = "Invalid job URL"
                        });

                var job = await service.ExtractAsync(url, cancellationToken);

                if (job is null)
                    return TypedResults.NotFound(
                        new ProblemDetails
                        {
                            Title = "Job could not be extracted"
                        });

                return TypedResults.Ok(job);
            });

        return app;
    }
}