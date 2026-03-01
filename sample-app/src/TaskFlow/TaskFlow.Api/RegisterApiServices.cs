using Application.Contracts.Constants;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using EF.AspNetCore;
using System.Threading.RateLimiting;

namespace TaskFlow.Api;

internal static class IServiceCollectionExtensions
{
    internal static readonly string[] healthCheckTagsFullDb = ["full", "db"];

    public static IServiceCollection RegisterApiServices(this IServiceCollection services, IConfiguration config, ILogger logger)
    {
        services.AddHttpContextAccessor();
        AddAuthentication(services, config, logger);
        AddErrorHandling(services);
        services.AddRouting(options => options.LowercaseUrls = true);
        AddOpenApiSupport(services, config);
        AddHealthChecks(services);
        AddCorrelationTracking(services);
        AddRateLimiting(services);
        return services;
    }

    private static void AddAuthentication(IServiceCollection services, IConfiguration config, ILogger logger)
    {
        string authConfigSectionName = "TaskFlowApi_EntraID";
        var configSection = config.GetSection(authConfigSectionName);
        if (!configSection.GetChildren().Any())
        {
            logger.LogInformation("No Auth Config ({ConfigSectionName}) Found; Auth will not be configured.", authConfigSectionName);
            services.AddAuthentication();
            services.AddAuthorization();
            return;
        }

        logger.LogInformation("Configure auth - {ConfigSectionName}", authConfigSectionName);
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddMicrosoftIdentityWebApi(config.GetSection(authConfigSectionName));

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build())
            .AddPolicy(AppConstants.ROLE_GLOBAL_ADMIN, policy => policy.RequireRole(AppConstants.ROLE_GLOBAL_ADMIN))
            .AddPolicy(AppConstants.ROLE_USER, policy => policy.RequireRole(AppConstants.ROLE_USER));
    }

    private static void AddErrorHandling(IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
                context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
                var activity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
                context.ProblemDetails.Extensions.TryAdd("activityId", activity?.Id);
            };
        });
    }

    private static void AddOpenApiSupport(IServiceCollection services, IConfiguration config)
    {
        services.AddOpenApi("v1");
    }

    private static void AddHealthChecks(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<TaskFlowDbContextTrxn>("TaskFlowDbContextTrxn", tags: healthCheckTagsFullDb)
            .AddDbContextCheck<TaskFlowDbContextQuery>("TaskFlowDbContextQuery", tags: healthCheckTagsFullDb);
    }

    private static void AddCorrelationTracking(IServiceCollection services)
    {
        services.AddHeaderPropagation(options => { options.Headers.Add("X-Correlation-ID"); });
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                });
            });
        });
    }
}
