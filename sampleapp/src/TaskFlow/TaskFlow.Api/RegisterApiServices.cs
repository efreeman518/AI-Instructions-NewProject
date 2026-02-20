// ═══════════════════════════════════════════════════════════════
// Pattern: API-specific service registration — things NOT in Bootstrapper.
// Auth, validation, routing, API versioning, OpenAPI, health checks, rate limiting.
// These are only needed by the API host, not by Scheduler, Functions, or Tests.
// ═══════════════════════════════════════════════════════════════

using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using TaskFlow.Api.Auth;
using TaskFlow.Api.ExceptionHandlers;
using TaskFlow.Api.HealthChecks;

namespace TaskFlow.Api;

/// <summary>
/// Pattern: API service registration — static class with extension method.
/// Separated from Bootstrapper because these concerns are API-host-specific.
/// </summary>
internal static class IServiceCollectionExtensions
{
    public static IServiceCollection RegisterApiServices(
        this IServiceCollection services, IConfiguration config, ILogger logger)
    {
        services.AddHttpContextAccessor();

        AddAuthentication(services, config, logger);
        AddErrorHandlingAndValidation(services);
        services.AddRouting(options => options.LowercaseUrls = true);

        var apiVersioningBuilder = AddApiVersioning(services);
        AddOpenApiSupport(services, config, apiVersioningBuilder);
        AddHealthChecks(services, config);
        AddRateLimiting(services);

        return services;
    }

    // ═══════════════════════════════════════════════════════════════
    // Authentication — Entra ID (service-to-service)
    // Pattern: MicrosoftIdentityWeb for JWT bearer auth.
    // X-Orig-Request header parsed for user identity (forwarded by Gateway).
    // ═══════════════════════════════════════════════════════════════

    private static void AddAuthentication(
        IServiceCollection services, IConfiguration config, ILogger logger)
    {
        // Pattern: Entra ID JWT bearer — configured from "AzureAd" config section.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(config.GetSection("AzureAd"));

        // Pattern: Authorization policies — TenantMatch validates route tenantId vs JWT claim.
        services.AddAuthorization(options =>
        {
            options.AddPolicy("TenantMatch", policy =>
                policy.AddRequirements(new TenantMatchRequirement()));

            options.AddPolicy("GlobalAdmin", policy =>
                policy.RequireRole("GlobalAdmin"));
        });

        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, TenantMatchHandler>();

        // Pattern: Gateway claims forwarding — X-Orig-Request header hydration.
        // GatewayClaimsTransformer reads the header and merges user claims into the principal.
        services.Configure<GatewayClaimsTransformSettings>(
            config.GetSection(GatewayClaimsTransformSettings.ConfigSectionName));
        services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, GatewayClaimsTransformer>();

        logger.LogInformation("Authentication configured with Entra ID JWT bearer + Gateway claims transformer.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Error Handling & Validation
    // Pattern: ProblemDetails for all errors + .NET 10 built-in validation.
    // ═══════════════════════════════════════════════════════════════

    private static void AddErrorHandlingAndValidation(IServiceCollection services)
    {
        // Pattern: Global exception handler — maps exceptions to ProblemDetails.
        services.AddExceptionHandler<DefaultExceptionHandler>();
        services.AddProblemDetails();

        // Pattern: .NET 10 built-in validation — replaces FluentValidation for simple cases.
        services.AddValidation();
    }

    // ═══════════════════════════════════════════════════════════════
    // API Versioning
    // Pattern: URL segment versioning — v{apiVersion:apiVersion}/...
    // ═══════════════════════════════════════════════════════════════

    private static IApiVersioningBuilder AddApiVersioning(IServiceCollection services)
    {
        return services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // OpenAPI / Scalar
    // Pattern: Enabled only when config says so — disabled in production.
    // ═══════════════════════════════════════════════════════════════

    private static void AddOpenApiSupport(
        IServiceCollection services, IConfiguration config, IApiVersioningBuilder versioningBuilder)
    {
        // Pattern: API versioning integration with API explorer for OpenAPI.
        versioningBuilder.AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        // Pattern: OpenAPI document generation — .NET 10 built-in.
        services.AddOpenApi();
    }

    // ═══════════════════════════════════════════════════════════════
    // Health Checks
    // Pattern: /health (full readiness) and /alive (liveness).
    // ═══════════════════════════════════════════════════════════════

    private static void AddHealthChecks(IServiceCollection services, IConfiguration config)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"])
            .AddCheck<AuthOidcHealthCheck>("auth-oidc", tags: ["ready"])
            .AddCheck<SqlTokenHealthCheck>("sql-token", tags: ["ready"])
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
                tags: ["live"]);
    }

    // ═══════════════════════════════════════════════════════════════
    // Rate Limiting
    // Pattern: Fixed-window rate limiter — protects against abuse.
    // ═══════════════════════════════════════════════════════════════

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("default", limiter =>
            {
                limiter.PermitLimit = 100;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 10;
            });
        });
    }
}
