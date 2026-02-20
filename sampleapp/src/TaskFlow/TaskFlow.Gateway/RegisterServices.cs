// ═══════════════════════════════════════════════════════════════
// Pattern: Gateway service registration — YARP, Auth, CORS, Health.
// YARP transforms: extract user claims → X-Orig-Request header,
// acquire service-to-service token via TokenService.
// Gateway has NO Bootstrapper dependency — it's a pure proxy.
// ═══════════════════════════════════════════════════════════════

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using TaskFlow.Gateway.Auth;
using TaskFlow.Gateway.HealthChecks;
using Yarp.ReverseProxy.Transforms;

namespace TaskFlow.Gateway;

internal static class RegisterServices
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: YARP registration with token relay transforms.
    // Loads routes/clusters from appsettings "ReverseProxy" section.
    // AddServiceDiscoveryDestinationResolver enables Aspire service discovery.
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection AddReverseProxyServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<TokenService>();

        services.AddReverseProxy()
            .LoadFromConfig(config.GetSection("ReverseProxy"))
            .AddTransforms(ConfigureProxyTransforms)
            .AddServiceDiscoveryDestinationResolver();

        return services;
    }

    /// <summary>
    /// Pattern: YARP request transform — token relay + claims forwarding.
    /// 1. Extract user JWT → serialize relevant claims → X-Orig-Request header.
    /// 2. Acquire client-credential token for downstream cluster → Authorization header.
    /// Auth flow: User → [External Token] → Gateway → [Client Credential + X-Orig-Request] → API.
    /// </summary>
    private static void ConfigureProxyTransforms(TransformBuilderContext context)
    {
        var tokenService = context.Services.GetRequiredService<TokenService>();
        var clusterId = context.Cluster?.ClusterId;
        if (string.IsNullOrEmpty(clusterId)) return;

        context.AddRequestTransform(async transformContext =>
        {
            // Pattern: Extract original user claims (if user token present) and forward as JSON.
            var authHeader = transformContext.HttpContext.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ") == true)
            {
                AddOriginalUserClaimsHeader(transformContext);
            }

            // Pattern: Acquire service-to-service token for downstream API.
            var token = await tokenService.GetAccessTokenAsync(clusterId);
            transformContext.ProxyRequest!.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        });
    }

    /// <summary>
    /// Pattern: Extract relevant claims from user JWT → serialize as JSON → X-Orig-Request header.
    /// The downstream API's IRequestContext reads this header to populate user identity.
    /// </summary>
    private static void AddOriginalUserClaimsHeader(RequestTransformContext context)
    {
        var authHeader = context.HttpContext.Request.Headers.Authorization.FirstOrDefault();
        var userToken = authHeader?["Bearer ".Length..];

        if (!string.IsNullOrEmpty(userToken))
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(userToken);

            var claims = new
            {
                sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value,
                oid = jwt.Claims.FirstOrDefault(c => c.Type == "oid")?.Value,
                email = jwt.Claims.FirstOrDefault(c => c.Type == "emails")?.Value,
                tenantId = jwt.Claims.FirstOrDefault(c => c.Type == "userTenantId")?.Value,
                roles = jwt.Claims.Where(c => c.Type == "extension_Roles").Select(c => c.Value).ToList()
            };

            context.ProxyRequest!.Headers.Add("X-Orig-Request", JsonSerializer.Serialize(claims));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: User-facing authentication — Entra External / B2C.
    // Gateway authenticates users; API uses service-to-service tokens.
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection AddGatewayAuthentication(this IServiceCollection services, IConfiguration config)
    {
        const string authConfigSectionName = "Gateway_EntraExt";

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddMicrosoftIdentityWebApi(config.GetSection(authConfigSectionName));

        // Pattern: Authorization handlers.
        services.AddSingleton<IAuthorizationHandler, TenantMatchHandler>();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build())
            .AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"))
            .AddPolicy("TenantMatchPolicy", policy => policy.AddRequirements(new TenantMatchRequirement()));

        // Pattern: Claims transformation — normalize B2C claims to standard types.
        services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, GatewayClaimsTransformer>();

        return services;
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: CORS — configured for the UI project's local and deployed URLs.
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection AddGatewayCors(this IServiceCollection services, IConfiguration config)
    {
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["https://localhost:5000", "https://localhost:7000"];

        services.AddCors(options =>
        {
            options.AddPolicy("GatewayPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Health checks — aggregate downstream service health.
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection AddGatewayHealthChecks(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(AggregateGatewayHealthCheck));
        services.AddHealthChecks()
            .AddCheck<AggregateGatewayHealthCheck>("downstream-services");

        return services;
    }
}
