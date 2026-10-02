# JobMatcher project instructions

## API contracts

- All ASP.NET Core endpoints must return typed results. Prefer `Results<...>` unions with specific result types such as `Ok<T>`, `BadRequest<ProblemDetails>`, and `ProblemHttpResult`; do not replace them with plain `IResult` unless there is a documented framework limitation.
- Every API endpoint must define descriptive `WithName`, `WithSummary`, and `WithDescription` metadata so Scalar displays clear documentation.
- Each endpoint class must group its routes under the class resource prefix with `MapGroup` (for example, job endpoints use `/api/jobs` and define the route as `/extract`, not `/api/jobs/extract`); keep all routes in that class within the group.
- Give each `MapGroup` a descriptive OpenAPI tag with `WithTags` so Scalar displays a readable resource group name.
- Inject endpoint loggers as typed `ILogger<EndpointLogger>` parameters (for example, `ILogger<JobEndpointLogger> logger`) instead of injecting `ILoggerFactory` and creating string-named loggers.
- Add new job-board integrations as `IJobExtractor` implementations with explicit host routing; generic web extractors must not run for a source that requires an authorized integration.
- Name private fields with a leading underscore and camelCase, for example `_initializationGate`.
- Preserve the public API response contract when changing implementation details.
- Use `ProblemDetails` for errors and keep status codes meaningful: `400` for invalid input, `422` for valid pages that contain no recognizable job, `502` for upstream/source failures, and `504` for timeouts.

## Line endings and file handling

- This is a Windows repository. All text files must use CRLF line endings so they open cleanly in Visual Studio without line-ending warnings.
- Preserve existing file encoding and formatting where possible. Do not introduce mixed line endings in a file.
- Use `apply_patch` for code edits. After patching, normalize edited text files to CRLF when necessary.
- Do not edit generated files under `bin/`, `obj/`, `.vs/`, or other build-output directories.

## Architecture

- Keep the dependency direction intact: `Domain` -> `Application` -> `Infrastructure` -> `Api`.
- Domain models should not depend on ASP.NET Core, Playwright, AngleSharp, or HTTP concerns.
- Put orchestration and contracts in `JobMatcher.Application`; put HTTP, browser, and HTML/JSON-LD implementations in `JobMatcher.Infrastructure`.
- Keep URL validation and extraction safety policies centralized rather than duplicating them in individual extractors.
- Preserve extractor fallback behavior: a recoverable failure in one extractor should allow the next applicable extractor to run.

## Job extraction behavior

- Treat arbitrary job URLs as untrusted input. Preserve scheme, host, redirect, timeout, response-size, cancellation, and concurrency protections.
- Do not log full URLs, query strings, credentials, raw HTML, job descriptions, or other sensitive page content.
- Keep extraction provenance, confidence, failure categories, and safe attempt diagnostics when changing the extraction model.
- Prefer structured JSON-LD data before rendered HTML. HTML fallback should remain conservative and should not silently claim high confidence.

## Dependencies and configuration

- Prefer focused package references over broad framework references in class-library projects.
- Put configurable extraction limits in `appsettings.json` and validate options at startup.
- Do not commit secrets or environment-specific `appsettings.*.json` files. The repository intentionally keeps the base `appsettings.json` trackable.

## Verification

- After code changes, run:

  ```powershell
  dotnet build JobMatcher.slnx --no-restore
  ```

- The build should finish with zero warnings and zero errors.
- Tests will live in a separate test project. When one exists, run `dotnet test JobMatcher.slnx` for behavior changes.
- Do not add live job-board calls to automated tests or verification commands.

## Documentation and task files

- The `docs/` directory is ignored by Git. Only update it when the user explicitly asks for documentation artifacts.
- Use `.tasks/` for implementation handoffs and task-specific instructions; keep those files focused and update their checklists when work is completed.
