// ═══════════════════════════════════════════════════════════════
// Pattern: Azure Functions isolated worker v4 — Program.cs.
// Shares Bootstrapper for DI but adds function-specific middleware
// and registrations. Loads appsettings.json BEFORE ConfigureFunctionsWebApplication.
// ═══════════════════════════════════════════════════════════════

using Azure.Identity;
using TaskFlow.FunctionApp;
using TaskFlow.FunctionApp.Infrastructure;
using TaskFlow.Bootstrapper;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string SERVICE_NAME = "TaskFlowFunctions";
ILogger<Program>? loggerStartup = null;

try
{
    var builder = FunctionsApplication.CreateBuilder(args);

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Load app-level config BEFORE ConfigureFunctionsWebApplication.
    // local.settings.json is for Functions runtime startup only.
    // appsettings.json is for app-level config (caching, service URLs, etc.).
    // ═══════════════════════════════════════════════════════════════
    builder.Configuration.AddJsonFile("appsettings.json", optional: true);
    var config = builder.Configuration;

    // Pattern: Static logger factory for startup logging (before DI is available).
    using var loggerFactory = LoggerFactory.Create(logBuilder =>
    {
        logBuilder.SetMinimumLevel(LogLevel.Information);
        logBuilder.AddConsole();
    });
    loggerStartup = loggerFactory.CreateLogger<Program>();
    loggerStartup.LogInformation("{AppName} - Startup", SERVICE_NAME);

    // Pattern: Required for HTTP triggers (FunctionHttpTrigger, FunctionHttpHealth).
    builder.ConfigureFunctionsWebApplication();

    // Pattern: DefaultAzureCredential — same as API host.
    var credentialOptions = new DefaultAzureCredentialOptions();
    var managedIdentityClientId = config.GetValue<string?>("ManagedIdentityClientId", null);
    if (managedIdentityClientId != null) credentialOptions.ManagedIdentityClientId = managedIdentityClientId;
    var credential = new DefaultAzureCredential(credentialOptions);

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Bootstrapper chain — shared DI registrations.
    // Functions share the same application/infrastructure services as the API.
    // ═══════════════════════════════════════════════════════════════
    builder.Services
        .RegisterDomainServices(config)
        .RegisterInfrastructureServices(config)
        .RegisterApplicationServices(config)
        .RegisterBackgroundServices(config);

    // Pattern: Function-app-specific registrations.
    builder.Services.Configure<Settings>(config.GetSection("Settings"));

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Middleware pipeline — exception handler wraps all invocations,
    // logger adds structured tracing per invocation.
    // ═══════════════════════════════════════════════════════════════
    builder.UseMiddleware<GlobalExceptionHandler>();
    builder.UseMiddleware<GlobalLogger>();

    var app = builder.Build();

    // Pattern: RunStartupTasks — same as API (migrations, cache warmup, etc.).
    await app.RunStartupTasks();
    await app.RunAsync();
}
catch (Exception ex)
{
    loggerStartup?.LogCritical(ex, "{ServiceName} - Host terminated unexpectedly", SERVICE_NAME);
}
finally
{
    loggerStartup?.LogInformation("{ServiceName} - Ending application", SERVICE_NAME);
}
