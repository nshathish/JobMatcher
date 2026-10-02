using JobMatcher.Api.Endpoints.Job;
using JobMatcher.Application.Jobs;
using JobMatcher.Infrastructure.Jobs;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddOptions<JobExtractionOptions>()
    .Bind(builder.Configuration.GetSection("JobExtraction"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JobExtractionOptions>>().Value);
builder.Services.AddSingleton<ExtractionConcurrencyLimiter>();
builder.Services.AddSingleton<JobExtractionMetrics>();
builder.Services.AddSingleton<PlaywrightBrowserManager>();

builder.Services.AddScoped<IJobExtractor, IndeedJobSourceAdapter>();
builder.Services.AddHttpClient<IJobExtractor, JsonLdJobExtractor>(client =>
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/154.0.0.0 Safari/537.36");

        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });
builder.Services.AddScoped<IJobExtractor, PlaywrightJobExtractor>();

builder.Services.AddScoped<JobExtractionService>();
builder.Services.AddScoped<JsonLdJobParser>();
builder.Services.AddScoped<HtmlJobParser>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => { options.DarkMode = false; });
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapGet("/api/health",
    static () => TypedResults.Ok(new { status = "healthy" }))
    .WithName("GetHealthStatus")
    .WithSummary("Check API health")
    .WithDescription("Returns the current health status of the JobMatcher API.");

app.MapJobEndpoints();


app.Run();
