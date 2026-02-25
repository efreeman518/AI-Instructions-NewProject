# API

## Overview

Use ASP.NET Core Minimal APIs with endpoint classes (no controllers), API versioning, `ProblemDetails`, and deterministic middleware ordering.

Reference implementation: `sampleapp/src/TaskFlow/TaskFlow.Api/`.

## Required Layout

```
{Host}.Api/
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
    .Map{Entity}Endpoints(includeErrorDetails);
```

## Endpoint Contract

Each entity exposes one static mapper: `Map{Entity}Endpoints(this IEndpointRouteBuilder group, ...)`.

```csharp
public static class {Entity}Endpoints
{
    public static void Map{Entity}Endpoints(this IEndpointRouteBuilder group, bool includeStack)
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

## Error Handling + Routes

- Global `IExceptionHandler` maps known exceptions (`400/404/...`) and defaults to `500`.
- Include stack traces only outside production.
- Conventions:
  - tenant CRUD: `v1/tenant/{tenantId}/{entity}/{id?}` + `TenantMatch`
  - global admin endpoints: admin role
  - diagnostics: `/health`, `/alive`
  - OpenAPI/Scalar: enabled only when `OpenApiSettings:Enable` is `true`

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
