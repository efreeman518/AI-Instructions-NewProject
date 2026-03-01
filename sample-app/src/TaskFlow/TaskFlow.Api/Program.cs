using Aspire.ServiceDefaults;
using Azure.Identity;
using TaskFlow.Api;
using TaskFlow.Bootstrapper;
using EF.Common;
using EF.Host;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;
var appName = config.GetValue<string>("AppName") ?? "TaskFlow.Api";
var env = config.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? config.GetValue<string>("DOTNET_ENVIRONMENT") ?? "Undefined";

ILogger<Program> startupLogger = CreateStartupLogger();
startupLogger.LogInformation("{AppName} {Environment} - Startup.", appName, env);

try
{
    startupLogger.LogInformation("{AppName} {Environment} - Configure service defaults.", appName, env);
    builder.AddServiceDefaults(config, appName);

    startupLogger.LogInformation("{AppName} {Environment} - Register services.", appName, env);
    services
        .RegisterInfrastructureServices(config)
        .RegisterDomainServices(config)
        .RegisterApplicationServices(config)
        .RegisterBackgroundServices(config)
        .RegisterApiServices(config, startupLogger);

    var app = builder.Build().ConfigurePipeline();

    startupLogger.LogInformation("{AppName} {Environment} - Running startup tasks.", appName, env);
    await app.RunStartupTasks();

    StaticLogging.SetStaticLoggerFactory(app.Services.GetRequiredService<ILoggerFactory>());

    startupLogger.LogInformation("{AppName} {Environment} - Running app.", appName, env);
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

ILogger<Program> CreateStartupLogger()
{
    StaticLogging.CreateStaticLoggerFactory(logBuilder =>
    {
        logBuilder.SetMinimumLevel(LogLevel.Information);
        logBuilder.AddConsole();
    });
    return StaticLogging.CreateLogger<Program>();
}

#pragma warning disable S1118
public partial class Program { }
#pragma warning restore S1118
