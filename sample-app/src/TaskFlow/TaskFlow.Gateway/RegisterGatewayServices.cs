using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace TaskFlow.Gateway;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection RegisterGatewayServices(this IServiceCollection services, IConfiguration config, ILogger logger)
    {
        services.AddHttpContextAccessor();
        AddAuthentication(services, config, logger);
        AddCors(services, config);
        AddYarpProxy(services, config);
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddHealthChecks();
        return services;
    }

    /// <summary>
    /// Configures JWT Bearer authentication using Microsoft Identity Web.
    /// Reads from the "TaskFlowGateway_EntraID" config section.
    /// If the section is missing/empty, auth is registered but NOT enforced (dev mode).
    ///
    /// To enable authentication for production:
    ///   1. Register the Gateway as an API app in Entra External ID (same tenant as the UI app)
    ///      - Expose an API scope (e.g., "api://taskflow-gateway/DefaultAccess")
    ///      - Grant the UI app's ClientId access to that scope
    ///
    ///   2. Add the "TaskFlowGateway_EntraID" section to Gateway appsettings.json:
    ///        "TaskFlowGateway_EntraID": {
    ///          "Instance": "https://&lt;tenant&gt;.ciamlogin.com/",
    ///          "TenantId": "&lt;tenant-id&gt;",
    ///          "ClientId": "&lt;gateway-client-id&gt;",
    ///          "Audience": "api://taskflow-gateway"
    ///        }
    ///
    ///   3. The fallback policy (RequireAuthenticatedUser) will auto-enforce on all
    ///      proxied routes. Health/alive endpoints are explicitly AllowAnonymous.
    /// </summary>
    private static void AddAuthentication(IServiceCollection services, IConfiguration config, ILogger logger)
    {
        string authConfigSectionName = "TaskFlowGateway_EntraID";
        var configSection = config.GetSection(authConfigSectionName);
        if (!configSection.GetChildren().Any())
        {
            // No Entra config found — register empty auth so the pipeline doesn't throw,
            // but no tokens will be validated (open access in dev mode).
            logger.LogInformation("No Auth Config ({ConfigSectionName}) Found; Auth will not be configured.", authConfigSectionName);
            services.AddAuthentication();
            services.AddAuthorization();
            return;
        }

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddMicrosoftIdentityWebApi(config.GetSection(authConfigSectionName));

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build());
    }

    private static void AddCors(IServiceCollection services, IConfiguration config)
    {
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
                else
                {
                    // Development fallback — allow any origin when none configured
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                }
            });
        });
    }

    private static void AddYarpProxy(IServiceCollection services, IConfiguration config)
    {
        services.AddReverseProxy()
            .LoadFromConfig(config.GetSection("ReverseProxy"))
            .AddServiceDiscoveryDestinationResolver();
    }
}
