using Aspire.ServiceDefaults;
using EF.Common;
using EF.Host;
using TaskFlow.Bootstrapper;
using TaskFlow.Scheduler;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;
var appName = config.GetValue<string>("AppName") ?? "TaskFlow.Scheduler";
var env = config.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? config.GetValue<string>("DOTNET_ENVIRONMENT") ?? "Undefined";

StaticLogging.CreateStaticLoggerFactory(logBuilder =>
{
    logBuilder.SetMinimumLevel(LogLevel.Information);
    logBuilder.AddConsole();
});
var startupLogger = StaticLogging.CreateLogger<Program>();
startupLogger.LogInformation("{AppName} {Environment} - Startup.", appName, env);

try
{
    builder.AddServiceDefaults(config, appName);

    services
        .RegisterInfrastructureServices(config)
        .RegisterDomainServices(config)
        .RegisterApplicationServices(config)
        .RegisterSchedulerServices(config, startupLogger);

    var app = builder.Build();

    await app.RunStartupTasks();

    app.MapHealthChecks("/health");
    app.MapGet("/alive", () => Results.Ok("Alive"));

    StaticLogging.SetStaticLoggerFactory(app.Services.GetRequiredService<ILoggerFactory>());
    await app.RunAsync();
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "{AppName} {Environment} - Host terminated unexpectedly.", appName, env);
}
finally
{
    startupLogger.LogInformation("{AppName} {Environment} - Ending application.", appName, env);
}

#pragma warning disable S1118
public partial class Program { }
#pragma warning restore S1118
