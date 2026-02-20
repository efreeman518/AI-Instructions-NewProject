// ═══════════════════════════════════════════════════════════════
// Pattern: Pipeline Configuration — ordered middleware + endpoint mapping.
// Pipeline order: HTTPS → Routing → RateLimiter → Auth → OpenAPI →
//                 ExceptionHandling → BasicEndpoints → ApiVersionedEndpoints
// ═══════════════════════════════════════════════════════════════

using Asp.Versioning;
using Scalar.AspNetCore;
using TaskFlow.Api.Endpoints;

namespace TaskFlow.Api;

/// <summary>
/// Pattern: Pipeline extension method — configures middleware order and maps endpoints.
/// Middleware order is critical: security before routing, routing before auth, auth before endpoints.
/// </summary>
public static partial class WebApplicationBuilderExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        var config = app.Configuration;

        SetupSecurityMiddleware(app, config);
        SetupOpenApi(app, config);
        SetupExceptionHandling(app);
        SetupBasicEndpoints(app);
        SetupApiVersionedEndpoints(app);

        return app;
    }

    // ═══════════════════════════════════════════════════════════════
    // Security Middleware
    // Pattern: HTTPS → Routing → RateLimiter → Authentication → Authorization
    // ═══════════════════════════════════════════════════════════════

    private static void SetupSecurityMiddleware(WebApplication app, IConfiguration config)
    {
        if (config.GetValue("EnforceHttpsRedirection", false))
            app.UseHttpsRedirection();

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
    }

    // ═══════════════════════════════════════════════════════════════
    // OpenAPI / Scalar UI
    // Pattern: Enabled per config — typically on in Development, off in Production.
    // ═══════════════════════════════════════════════════════════════

    private static void SetupOpenApi(WebApplication app, IConfiguration config)
    {
        if (config.GetValue("OpenApiSettings:Enable", false))
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("TaskFlow API")
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.AsyncHttp);
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Exception Handling
    // Pattern: Global exception handler using IExceptionHandler pipeline.
    // ═══════════════════════════════════════════════════════════════

    private static void SetupExceptionHandling(WebApplication app)
    {
        app.UseExceptionHandler();
    }

    // ═══════════════════════════════════════════════════════════════
    // Basic Endpoints (non-versioned)
    // Pattern: Health checks at /health (readiness) and /alive (liveness).
    // ═══════════════════════════════════════════════════════════════

    private static void SetupBasicEndpoints(WebApplication app)
    {
        // Pattern: Aspire-compatible health endpoints.
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        app.MapHealthChecks("/alive", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // API Versioned Endpoints
    // Pattern: MapGroup with version set + tenant scope + authorization policy.
    // Each entity gets its own Map{Entity}Endpoints extension method.
    // ═══════════════════════════════════════════════════════════════

    private static void SetupApiVersionedEndpoints(WebApplication app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        var includeErrorDetails = !app.Environment.IsProduction();

        // Pattern: Tenant-scoped routes — tenantId in path, TenantMatch policy validates JWT claim.
        // TodoItems — full CRUD + Search + Lookup
        app.MapGroup("v{apiVersion:apiVersion}/tenant/{tenantId}/todoitems")
            .WithApiVersionSet(apiVersionSet)
            .RequireAuthorization("TenantMatch")
            .MapTodoItemEndpoints(includeErrorDetails);

        // Categories — tenant-scoped, cacheable static data
        app.MapGroup("v{apiVersion:apiVersion}/tenant/{tenantId}/categories")
            .WithApiVersionSet(apiVersionSet)
            .RequireAuthorization("TenantMatch")
            .MapCategoryEndpoints(includeErrorDetails);

        // Tags — global (no tenant), shared across all tenants
        app.MapGroup("v{apiVersion:apiVersion}/tags")
            .WithApiVersionSet(apiVersionSet)
            .RequireAuthorization()
            .MapTagEndpoints(includeErrorDetails);

        // Teams — tenant-scoped with child TeamMembers
        app.MapGroup("v{apiVersion:apiVersion}/tenant/{tenantId}/teams")
            .WithApiVersionSet(apiVersionSet)
            .RequireAuthorization("TenantMatch")
            .MapTeamEndpoints(includeErrorDetails);
    }
}
