// ═══════════════════════════════════════════════════════════════
// Pattern: Scheduler Host Program.cs — TickerQ-based cron scheduler.
// Separate host from the API — runs as its own ASP.NET Core app.
// Shares Bootstrapper for infrastructure/application DI but registers
// its own TickerQ-specific services.
// ═══════════════════════════════════════════════════════════════

using Aspire.ServiceDefaults;
using Azure.Identity;
using TaskFlow.Bootstrapper;
using TaskFlow.Scheduler;
using TickerQ.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;
var appName = config.GetValue<string>("AppName") ?? "TaskFlow.Scheduler";
var credential = new DefaultAzureCredential();

try
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: Aspire service defaults (OpenTelemetry, health checks, service discovery)
    // ═══════════════════════════════════════════════════════════════
    builder.AddServiceDefaults();

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Bootstrapper chain — same shared DI as API host.
    // Scheduler shares infrastructure + application services with the API
    // but registers its own TickerQ-specific services.
    // ═══════════════════════════════════════════════════════════════
    services
        .RegisterInfrastructureServices(config)
        .RegisterApplicationServices(config)
        .RegisterSchedulerServices(config);

    // ═══════════════════════════════════════════════════════════════
    // Pattern: TickerQ configuration — persistence, dashboard, scheduler settings.
    // Separated into RegisterSchedulerServices.AddTickerQConfig extension.
    // ═══════════════════════════════════════════════════════════════
    builder.AddTickerQConfig();

    var app = builder.Build();

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Verify TickerQ database schema on startup.
    // TickerQ does NOT use EF Core migrations — it manages its own schema.
    // ═══════════════════════════════════════════════════════════════
    await app.ConfigureTickerQDatabase(config, app.Services.GetRequiredService<ILogger<Program>>());

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Pipeline — TickerQ middleware + basic endpoints.
    // ═══════════════════════════════════════════════════════════════
    app.UseTickerQ();
    app.MapOpenApi();
    app.MapDefaultEndpoints();

    // Pattern: Root redirect to health endpoint.
    app.MapGet("/", () => Results.Redirect("/health"));

    // Pattern: Status endpoint for monitoring — returns scheduler config.
    app.MapGet("/api/scheduler/status", () => Results.Ok(new
    {
        scheduler = appName,
        configuration = new
        {
            persistence = config.GetValue<bool>("Scheduling:UsePersistence", true),
            dashboard = config.GetValue<bool>("Scheduling:EnableDashboard", true),
            pollIntervalSeconds = config.GetValue<int>("Scheduling:PollIntervalSeconds", 30)
        },
        endpoints = new { health = "/health", dashboard = "/scheduler" }
    }));

    await app.RunAsync();
}
catch (Exception ex)
{
    // Pattern: Startup failure logging — same pattern as API host.
    Console.Error.WriteLine($"[{appName}] Host terminated unexpectedly: {ex.Message}");
    throw;
}
