# API

## Overview

The API project uses **ASP.NET Core Minimal APIs** with endpoint classes (not controllers), API versioning, structured error handling via ProblemDetails, and a clean pipeline configuration.

## Project Structure

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Api/` for the complete API project layout.

```
{Host}.Api/
├── Program.cs                        # Entry point, service registration chain, startup
├── RegisterApiServices.cs            # API-specific DI (auth, versioning, OpenAPI, health)
├── WebApplicationBuilderExtensions.cs # Pipeline configuration (middleware, endpoint mapping)
├── Endpoints/                        # One file per entity/aggregate
│   ├── {Entity}Endpoints.cs
│   └── ...
├── Auth/                             # Auth configuration, claims transformers
│   ├── GatewayClaimsTransformer.cs
│   └── TenantMatchHandler.cs
├── ExceptionHandlers/
│   └── DefaultExceptionHandler.cs
├── HealthChecks/
│   └── CustomHealthCheck.cs
├── Middleware/
│   └── CustomHeaderAuthMiddleware.cs
├── appsettings.json
├── appsettings.Development.json
├── Dockerfile
└── Properties/launchSettings.json
```

## Program.cs Pattern

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Api/Program.cs`

The host entry point follows this structure: Aspire service defaults → configuration loading → Bootstrapper chain + API-specific registrations → build → pipeline → startup tasks → run. Wrap in try/catch/finally with structured startup logging.

```csharp
// Condensed pattern — see sampleapp for full implementation
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

## API Service Registration

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Api/RegisterApiServices.cs`

`RegisterApiServices.cs` handles API-specific concerns: auth, API versioning, OpenAPI/Scalar, health checks, rate limiting, and correlation tracking. It also registers `IClaimsTransformation` for Gateway claims forwarding.

## Pipeline Configuration

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Api/WebApplicationBuilderExtensions.cs`

`WebApplicationBuilderExtensions.cs` configures middleware order (HTTPS → routing → rate limiter → auth → authorization) and maps versioned endpoint groups. Tenant-scoped routes use `RequireAuthorization("TenantMatch")`; global admin routes require `AppConstants.ROLE_GLOBAL_ADMIN`.

```csharp
// Condensed pattern — see sampleapp for full pipeline
private static void SetupApiVersionedEndpoints(WebApplication app)
{
    var apiVersionSet = app.NewApiVersionSet()
        .HasApiVersion(new ApiVersion(1, 0))
        .ReportApiVersions()
        .Build();

    app.MapGroup("v{apiVersion:apiVersion}/tenant/{tenantId}/{entity}")
        .WithApiVersionSet(apiVersionSet)
        .RequireAuthorization("TenantMatch")
        .Map{Entity}Endpoints(includeErrorDetails);
}
```

## Endpoint Pattern

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Api/Endpoints/TodoItemEndpoints.cs` (full CRUD), `CategoryEndpoints.cs`, `TagEndpoints.cs`, `TeamEndpoints.cs`

Each entity gets a static endpoint class with `Map{Entity}Endpoints`. The pattern uses `TypedResults` for type-safe responses and `Result.Match()` to map service results to HTTP responses consistently.

```csharp
// Condensed pattern — see sampleapp for full implementation
public static class {Entity}Endpoints
{
    public static void Map{Entity}Endpoints(this IEndpointRouteBuilder group, bool problemDetailsIncludeStackTrace)
    {
        group.MapPost("/search", Search).Produces<PagedResponse<{Entity}Dto>>(StatusCodes.Status200OK);
        group.MapGet("/{id:guid}", GetById).Produces<DefaultResponse<{Entity}Dto>>(StatusCodes.Status200OK);
        group.MapPost("/", Create).Produces<DefaultResponse<{Entity}Dto>>(StatusCodes.Status201Created);
        group.MapPut("/{id:guid}", Update).Produces<DefaultResponse<{Entity}Dto>>();
        group.MapDelete("/{id:guid}", Delete).Produces(StatusCodes.Status204NoContent);
    }

    // Result.Match maps service outcome to HTTP result:
    private static async Task<IResult> GetById([FromServices] I{Entity}Service service, Guid id)
    {
        var result = await service.GetAsync(id);
        return result.Match<IResult>(
            response => TypedResults.Ok(response),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(...)),
            () => TypedResults.NotFound(id));
    }
}
```

### Endpoint Rules

1. **Static class with static methods** — No instance state except `_problemDetailsIncludeStackTrace`
2. **`TypedResults`** — Use `TypedResults.Ok()`, `TypedResults.Created()`, etc. for type-safe responses
3. **`Result.Match()`** — Maps service Result to HTTP responses consistently
4. **ProblemDetails for all errors** — Never return raw error strings
5. **URL/body ID validation** — Check on Update that route and body IDs match
6. **OpenAPI metadata** — `.Produces<>()`, `.ProducesProblem()`, `.WithSummary()`
7. **Search uses POST** — Complex filter payloads in body, not query params

## Exception Handler

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Api/ExceptionHandlers/DefaultExceptionHandler.cs`

Global exception handler using `IExceptionHandler`. Maps exception types to appropriate HTTP status codes (ValidationException → 400, NotFoundException → 404, default → 500) and writes structured `ProblemDetails` responses. Stack traces are included only in non-production environments.

## Route Conventions

| Pattern | Example | Auth |
|---------|---------|------|
| Tenant-scoped CRUD | `v1/tenant/{tenantId}/client/{id}` | TenantMatch policy |
| Global admin | `v1/tenant/{id}` | GlobalAdmin role |
| Health/liveness | `/alive`, `/health` | Anonymous |
| OpenAPI | `/openapi/v1.json`, `/scalar/v1` | Per config |

## OpenAPI / Scalar

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Api/WebApplicationBuilderExtensions.cs` — `SetupOpenApi` method

OpenAPI and Scalar UI are enabled conditionally via `OpenApiSettings:Enable`. Scalar uses `WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.AsyncHttp)`.

---

## Verification

After generating API endpoints and host setup, confirm:

- [ ] `Program.cs` calls `AddBootstrapper()` then adds API-specific concerns (auth, OpenAPI, health)
- [ ] Endpoints are static classes under `Endpoints/{Entity}/` with `Map{Entity}Endpoints(this IEndpointRouteBuilder)` extension
- [ ] Route pattern: `{entity-route}` (kebab-case) with group prefix and `.RequireAuthorization()`
- [ ] All endpoints accept `CancellationToken` as the last parameter
- [ ] Validation errors return `Results.ValidationProblem()`, not raw exceptions
- [ ] Swagger/Scalar documentation enabled only when `OpenApiSettings:Enable` is true
- [ ] Health endpoints: `/health` (full) and `/alive` (liveness) are mapped
- [ ] Auth uses `AddMicrosoftIdentityWebApi` with Entra ID section (service-to-service, not user-facing)
- [ ] `X-Orig-Request` header is parsed for user identity (forwarded by Gateway)
- [ ] Cross-references: Endpoint methods match [endpoint-template.md](../templates/endpoint-template.md), service interfaces match [application-layer.md](application-layer.md)
