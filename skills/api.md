# API

> **When to read:** Phase 5b, when building ASP.NET Core Minimal API endpoint groups, `ProblemDetails` exception handling, or the API host's middleware pipeline.
> **Skip if:** No API host in scope (e.g., scheduler-only or function-app-only scaffold); UI work; gateway-only work (see `gateway.md`).

## Worked Example

This is `TaskItemEndpoints.cs` from TaskFlow (`../AI-Instructions-ReferenceApp/src/Host/TaskFlow.Api/Endpoints/TaskItemEndpoints.cs`) — full route group with one handler shown. Demonstrates `MapGroup`, `Produces`, `Result.Match`, and `ProblemDetailsHelper` usage.

```csharp
public static class TaskItemEndpoints
{
    private static bool _problemDetailsIncludeStackTrace;

    public static IEndpointRouteBuilder MapTaskItemEndpoints(
        this IEndpointRouteBuilder group, bool problemDetailsIncludeStackTrace)
    {
        _problemDetailsIncludeStackTrace = problemDetailsIncludeStackTrace;

        var g = group.MapGroup("/api/task-items").WithTags("TaskItems");

        g.MapPost("/search", Search)
            .Produces<PagedResponse<TaskItemDto>>(StatusCodes.Status200OK)
            .WithSummary("Search TaskItems with paging, filters, and sorts");

        g.MapGet("/{id:guid}", GetById)
            .Produces<DefaultResponse<TaskItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a single TaskItem");

        g.MapPost("/", Create)
            .Produces<DefaultResponse<TaskItemDto>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithSummary("Create a new TaskItem");

        // ... PUT, DELETE follow the same shape

        return group;
    }

    private static async Task<IResult> GetById(
        [FromServices] ITaskItemService service, Guid id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return result.Match<IResult>(
            response => TypedResults.Ok(response),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, statusCodeOverride: StatusCodes.Status400BadRequest)),
            () => TypedResults.NotFound(id));
    }
}
```

Things to notice:
- Static class with extension method (`MapTaskItemEndpoints`) — no controllers, no instance state.
- `MapGroup` carries the route prefix and `WithTags` for OpenAPI grouping. Authorization (`.RequireAuthorization(...)`) typically applies at the group level in the host's `Program.cs`.
- Handlers are `private static` methods. Service is injected via `[FromServices]`. `CancellationToken` is the last parameter on every handler.
- `result.Match<IResult>(success, failure, none)` maps `Result<T>` directly to `IResult` — three branches for the three terminal states.
- `ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(...)` is the canonical way to surface multi-error failures; single-error failures use the singular variant.
- Stack trace inclusion is controlled by configuration (`_problemDetailsIncludeStackTrace`), not by environment checks.

The principles below are commentary on this shape.

## Overview

Use ASP.NET Core Minimal APIs with endpoint classes (no controllers), `ProblemDetails`, and deterministic middleware ordering.

Reference patterns: [../patterns/api-host-wiring.md](../patterns/api-host-wiring.md) (API Startup Sequence, Middleware Pipeline).

## Required Layout

```
Host/{Host}.Api/
├── Program.cs
├── RegisterApiServices.cs
├── WebApplicationBuilderExtensions.cs
├── Endpoints/{Entity}Endpoints.cs
├── Auth/
├── HealthChecks/
└── Middleware/
```

## Startup Pattern

In `Program.cs`, keep this order: service defaults/config → bootstrapper + API registration → build → pipeline → startup tasks → run.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Accept enum names as strings from all callers (e.g. "High" not 3).
// Without this, minimal-API endpoints reject string enum values with BadHttpRequestException.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.AddServiceDefaults();
services
    .RegisterInfrastructureServices(config)
    .RegisterDomainServices(config)
    .RegisterApplicationServices(config)
    .RegisterBackgroundServices(config)
    .AddApiServices(config, startupLogger);

var app = builder.Build().ConfigurePipeline();
await app.RunStartupTasks();
await app.RunAsync();
```

### Standalone CORS (Without Aspire)

When the API runs without Aspire orchestration (e.g. `dotnet run --launch-profile https` directly), the browser WASM client cannot reach it without an explicit CORS policy. Add this before authentication middleware:

```csharp
const string UiPolicy = "UiCors";
builder.Services.AddCors(options =>
    options.AddPolicy(UiPolicy, policy =>
        policy.WithOrigins("https://localhost:{uiPort}", "http://localhost:{uiPort}")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// In pipeline, before UseAuthentication:
app.UseCors(UiPolicy);
```

Note: Aspire-hosted deployments typically handle CORS through the gateway — only add direct API CORS for standalone dev scenarios.

## Service Registration

`RegisterApiServices.cs` owns API-only concerns (public method: `AddApiServices`):

- Entra auth + authorization policies (admin role, user role)
- `DefaultExceptionHandler` + `ProblemDetails`
- OpenAPI/Scalar (feature-flagged via `OpenApiSettings:Enable`)
- Rate limiting
- `IClaimsTransformation` for Gateway-forwarded claims

## Pipeline + Versioned Groups

`WebApplicationBuilderExtensions.cs` must preserve middleware order:
SecurityHeaders → CorrelationId → ExceptionHandler → RateLimiter → CORS → Authentication → Authorization.

Map versioned groups and apply policy at the group level (adjust route pattern to project needs — tenant-scoped, versioned, or simple `/api/` prefix):

```csharp
// Simple pattern (default)
app.MapGroup("/api/categories")
    .WithTags("Categories")
    .RequireAuthorization()
    .MapCategoryEndpoints(problemDetailsIncludeStackTrace);

// Versioned + tenant-scoped pattern (when needed)
var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.MapGroup("v{apiVersion:apiVersion}/tenant/{tenantId}/{entity}")
    .WithApiVersionSet(apiVersionSet)
    .RequireAuthorization("TenantMatch")
    .Map{Entity}Endpoints(problemDetailsIncludeStackTrace);
```

## Endpoint Contract

Each entity exposes one static mapper: `Map{Entity}Endpoints(this IEndpointRouteBuilder group, ...)`.

```csharp
public static class {Entity}Endpoints
{
    public static void Map{Entity}Endpoints(this IEndpointRouteBuilder group, bool problemDetailsIncludeStackTrace)
    {
        group.MapPost("/search", Search);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
    }
}
```

Required endpoint rules:

1. Static class + static handlers.
2. Use `TypedResults` for success responses.
3. Use `Result.Match()` (or equivalent single mapping path) for service outcomes.
4. Return `ProblemDetails` for errors (no raw strings).
5. Validate route/body ID consistency on update.
6. Add OpenAPI metadata (`Produces*`, summary/tags).
7. Use POST for complex search filters.

## Error Handling Strategy

Two complementary layers — **Result pattern for expected outcomes, `DefaultExceptionHandler` for unexpected exceptions**:

1. **Result flow (primary path):** Services return `Result<T>` / `DomainResult<T>`. Endpoints use `Result.Match()` to map success/failure/not-found to `TypedResults` + `ProblemDetails`. No exceptions thrown for validation, business rules, or not-found cases.
2. **`DefaultExceptionHandler` (safety net):** A global `IExceptionHandler` registered via `AddExceptionHandler<DefaultExceptionHandler>()`. Catches only truly unexpected exceptions (null refs, timeouts, infra failures) and maps them to `ProblemDetails` with appropriate HTTP status codes. This is a last-resort handler, not a control-flow mechanism. See [exception-handler-template](../templates/exception-handler-template.md) for implementation.

Reference: See [exception-handler-template](../templates/exception-handler-template.md) for the implementation pattern.

### Error Pipeline Overview

```
[Domain]   DomainResult<T>.Success / .Failure     — business validation, rules, state transitions
               ↓
[Service]  Result<T>.Success / .Failure / .None    — orchestration, tenant boundary, structure validation
               ↓
[Endpoint] result.Match(ok → TypedResults, errors → ProblemDetails, notFound → NotFound)
               ↓
[Global]   DefaultExceptionHandler (IExceptionHandler)  — unexpected exceptions only → ProblemDetails
```

### Error Type Mapping

| Source | Error | HTTP Status |
|---|---|---|
| Domain | `DomainError.Validation` | 400 Bad Request |
| Domain | `DomainError.NotFound` | 404 Not Found |
| Domain | `DomainError.Conflict` | 409 Conflict |
| Domain | `DomainError.Unauthorized` | 403 Forbidden |
| Service | `Result.Failure` (generic) | 422 Unprocessable Entity |
| Service | `Result.None` | 404 Not Found |
| Service | `StructureValidator` failure | 400 Bad Request |
| Global | `DbUpdateConcurrencyException` | 409 Conflict |
| Global | `UnauthorizedAccessException` | 403 Forbidden |
| Global | `OperationCanceledException` | 499 Client Closed (non-standard; log only — response may not be written) |
| Global | Unhandled exception | 500 Internal Server Error |

### Anti-Patterns

- **Throwing exceptions for business logic** — Use `DomainResult.Failure()` / `Result.Failure()` instead.
- **Swallowing errors silently** — Every failure must propagate through the Result chain or be logged explicitly.
- **Returning raw error strings** — Always wrap in `ProblemDetails` at the API boundary.
- **Catching generic `Exception` in services** — Let `DefaultExceptionHandler` handle the rest.
- **Exposing stack traces in production** — Only include outside production.

## OpenAPI / Scalar Configuration

Gate OpenAPI and Scalar behind `OpenApiSettings:Enable` in appsettings:

```csharp
if (config.GetValue<bool>("OpenApiSettings:Enable"))
{
    services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, ct) =>
        {
            document.Info = new()
            {
                Title = "{App} API",
                Version = "v1",
                Description = "Multi-tenant {App} API"
            };
            return Task.CompletedTask;
        });
    });
}
```

In `ConfigurePipeline()`:

```csharp
if (app.Configuration.GetValue<bool>("OpenApiSettings:Enable"))
{
    app.MapOpenApi();           // serves /openapi/v1.json
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("{App} API");
        options.WithTheme(ScalarTheme.Moon);
    });
}
```

Endpoint OpenAPI metadata — add to every endpoint:

```csharp
group.MapGet("/{id:guid}", GetById)
    .WithName("Get{Entity}ById")
    .WithSummary("Get a single {Entity} by ID")
    .WithTags("{Entity}")
    .Produces<{Entity}Dto>(200)
    .ProducesProblem(404)
    .ProducesProblem(401);
```

- Enable XML comments in the `.csproj`: `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.
- Add `/// <summary>` to handler methods for auto-generated descriptions.
- Never expose OpenAPI/Scalar in production unless explicitly required.

---

## Verification

- [ ] `Program.cs` wires bootstrapper + API services before `Build()`
- [ ] Endpoints are static classes under `Endpoints/` with `Map{Entity}Endpoints(...)`
- [ ] Route groups apply `.RequireAuthorization(...)` at the correct scope
- [ ] All handlers accept `CancellationToken` as last parameter
- [ ] Validation and business errors return `ProblemDetails`/`ValidationProblem`
- [ ] Swagger/Scalar is gated by `OpenApiSettings:Enable`
- [ ] `/health` and `/alive` are both mapped
- [ ] Entra auth uses `AddMicrosoftIdentityWebApi` (service-to-service)
- [ ] `X-Orig-Request` forwarding is parsed for identity context
- [ ] Cross-check with [endpoint-template.md](../templates/endpoint-template.md) and [application-layer.md](application-layer.md)

---

**TaskFlow proof (local):** `../AI-Instructions-ReferenceApp/src/Host/TaskFlow.Api/RegisterApiServices.cs` + `../AI-Instructions-ReferenceApp/src/Host/TaskFlow.Api/Endpoints/TaskItemEndpoints.cs`
**TaskFlow proof (remote fallback):** <https://github.com/efreeman518/AI-Instructions-ReferenceApp/blob/main/src/Host/TaskFlow.Api/Endpoints/TaskItemEndpoints.cs>
