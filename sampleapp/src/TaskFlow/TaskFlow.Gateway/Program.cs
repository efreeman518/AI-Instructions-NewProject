// ═══════════════════════════════════════════════════════════════
// Pattern: Gateway Program.cs — YARP reverse proxy entry point.
// Authenticates users (Entra External/B2C), acquires service-to-service
// tokens for downstream APIs, forwards original claims via X-Orig-Request.
// ═══════════════════════════════════════════════════════════════

using Aspire.ServiceDefaults;
using TaskFlow.Gateway;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;
var appName = config.GetValue<string>("AppName") ?? "TaskFlow.Gateway";

try
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: Aspire service defaults (OpenTelemetry, health checks, service discovery).
    // ═══════════════════════════════════════════════════════════════
    builder.AddServiceDefaults();

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Gateway-specific service registration.
    // YARP, auth, CORS, health checks — no Bootstrapper (Gateway has
    // no domain/application dependency, it's a pure proxy).
    // ═══════════════════════════════════════════════════════════════
    services
        .AddReverseProxyServices(config)
        .AddGatewayAuthentication(config)
        .AddGatewayCors(config)
        .AddGatewayHealthChecks();

    var app = builder.Build();

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Pipeline order — Security → CORS → Routing → RateLimiter → Auth → Endpoints → ReverseProxy.
    // ═══════════════════════════════════════════════════════════════
    app.ConfigurePipeline();

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[{appName}] Host terminated unexpectedly: {ex.Message}");
    throw;
}
