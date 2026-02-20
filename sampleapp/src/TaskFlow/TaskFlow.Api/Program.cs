// ═══════════════════════════════════════════════════════════════
// Pattern: API Program.cs — thin entry point with try/catch/finally startup.
// Delegates all DI to Bootstrapper + API-specific RegisterApiServices.
// Pipeline configured via WebApplicationBuilderExtensions.ConfigurePipeline().
// Startup tasks run after Build() and before RunAsync().
// ═══════════════════════════════════════════════════════════════

using TaskFlow.Api;
using TaskFlow.Bootstrapper;
using Package.Infrastructure.Common;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;
var appName = config.GetValue<string>("AppName") ?? "TaskFlow.Api";
var env = config.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Undefined";

// Pattern: Create a startup logger before DI is ready — for logging during configuration.
ILogger<Program> startupLogger = LoggerFactory
    .Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information))
    .CreateLogger<Program>();

startupLogger.LogInformation("{AppName} {Environment} — Startup.", appName, env);

try
{
    // Pattern: Aspire service defaults — telemetry, resilience, health, service discovery.
    builder.AddServiceDefaults();

    // ═══════════════════════════════════════════════════════════════
    // Service Registration Chain
    // Pattern: Bootstrapper handles cross-cutting DI, API adds host-specific concerns.
    // ═══════════════════════════════════════════════════════════════

    services
        .RegisterInfrastructureServices(config)
        .RegisterDomainServices(config)
        .RegisterApplicationServices(config)
        .RegisterBackgroundServices(config)
        .RegisterApiServices(config, startupLogger);

    // ═══════════════════════════════════════════════════════════════
    // Build + Configure Pipeline + Startup Tasks
    // ═══════════════════════════════════════════════════════════════

    var app = builder.Build().ConfigurePipeline();

    // Pattern: Run startup tasks — migrations, cache warmup, handler registration.
    await app.RunStartupTasks();

    // Pattern: Set static logger factory for non-DI contexts (static helpers, etc.).
    StaticLogging.SetStaticLoggerFactory(app.Services.GetRequiredService<ILoggerFactory>());

    startupLogger.LogInformation("{AppName} {Environment} — Ready to accept requests.", appName, env);

    await app.RunAsync();
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "{AppName} {Environment} — Host terminated unexpectedly.", appName, env);
}
finally
{
    startupLogger.LogInformation("{AppName} {Environment} — Ending application.", appName, env);
}
