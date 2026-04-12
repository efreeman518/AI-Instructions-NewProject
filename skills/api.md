# API

## Overview

Use ASP.NET Core Minimal APIs with endpoint classes (no controllers), API versioning, `ProblemDetails`, and deterministic middleware ordering.

Reference patterns: [../patterns/api-host-wiring.md](../patterns/api-host-wiring.md) (API Startup Sequence, Middleware Pipeline).

## Required Layout

```
Host/{Host}.Api/
├── Program.cs
├── RegisterApiServices.cs
├── WebApplicationBuilderExtensions.cs
├── Endpoints/{Entity}Endpoints.cs
├── Auth/
├── ExceptionHandlers/
├── HealthChecks/
└── Middleware/
```

## Startup Pattern

In `Program.cs`, keep this order: service defaults/config → bootstrapper + API registration → build → pipeline → startup tasks → run.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(config, appName);
services
    .RegisterInfrastructureServices(config)
    .RegisterApplicationServices(config)
    .RegisterApiServices(config, startupLogger);

var app = builder.Build().ConfigurePipeline();
await app.RunStartupTasks();
await app.RunAsync();
```

## Service Registration

`RegisterApiServices.cs` owns API-only concerns:

- Entra auth + authorization policies (`TenantMatch`, admin role)
- API versioning
- OpenAPI/Scalar (feature-flagged)
- health checks, rate limiting, correlation
- `IClaimsTransformation` for Gateway-forwarded claims

## Pipeline + Versioned Groups

`WebApplicationBuilderExtensions.cs` must preserve middleware order:
HTTPS → routing → rate limiter → authentication → authorization.

Map versioned groups and apply policy at the group level:

```csharp
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
- **Catching generic `Exception` in services** — Catch only specific exceptions (e.g., `DbUpdateConcurrencyException`). Let `DefaultExceptionHandler` handle the rest.

- Include stack traces only outside production.
- Conventions:
  - tenant CRUD: `v1/tenant/{tenantId}/{entity}/{id?}` + `TenantMatch`
  - global admin endpoints: admin role
  - diagnostics: `/health`, `/alive`
  - OpenAPI/Scalar: enabled only when `OpenApiSettings:Enable` is `true`

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
        options.WithTheme(ScalarTheme.BluePlanet);
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

## End-to-End Error Flow Example

Validation error trace: `Client POST → Endpoint → Service.Create → Domain.Create → StructureValidator fails → DomainResult.Failure → Result.Failure → Endpoint result.Match → 400 + ProblemDetails`.

`DefaultExceptionHandler` is **never invoked** for business errors — it only catches unexpected exceptions that escape the Result pattern.

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
