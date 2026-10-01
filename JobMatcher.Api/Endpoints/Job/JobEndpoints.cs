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
                Ok<ExtractedJob>,
                BadRequest<ProblemDetails>,
                NotFound<ProblemDetails>>> (
                ExtractJobRequest? request,
                JobExtractionService service,
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
