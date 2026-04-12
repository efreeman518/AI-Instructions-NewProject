# API Host Wiring Patterns

Cross-project wiring for API host startup, request context, and conditional auth. Load before Phase 5b (App Core) and Phase 5c (Runtime/Edge).

For base types used here, see [../support/ef-packages-reference.md](../support/ef-packages-reference.md).

---

## API Startup Sequence

**Source:** `Host/{App}.Api/Program.cs`

The startup follows a strict order: early logger, service registration chain, build, pipeline, startup tasks, run. The entire body is wrapped in try/catch/finally using a `StaticLogging` logger created before the host exists.

```csharp
var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;
var appName = config.GetValue<string>("AppName") ?? "{App}.Api";
var env = config.GetValue<string>("ASPNETCORE_ENVIRONMENT")
    ?? config.GetValue<string>("DOTNET_ENVIRONMENT") ?? "Undefined";

ILogger<Program> startupLogger = CreateStartupLogger();
startupLogger.LogInformation("{AppName} {Environment} - Startup.", appName, env);

try
{
    // 1. Service defaults (OpenTelemetry, health, resilience)
    builder.AddServiceDefaults(config, appName);

    // 2. Registration chain -- order matters for dependency resolution
    services
        .RegisterInfrastructureServices(config)  // config, caching, DB, request context, startup tasks
        .RegisterDomainServices(config)          // domain-specific registrations
        .RegisterApplicationServices(config)     // message handlers, app services
        .RegisterBackgroundServices(config)      // channel background queue
        .RegisterApiServices(config, startupLogger); // auth, routing, health checks, rate limiting

    // 3. Build + pipeline
    var app = builder.Build().ConfigurePipeline();

    // 4. Startup tasks (migrations, seeding, etc.)
    await app.RunStartupTasks();

    // 5. Switch to runtime logger
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
```

**Early logger factory** -- created before the host so startup failures are visible:

```csharp
ILogger<Program> CreateStartupLogger()
{
    StaticLogging.CreateStaticLoggerFactory(logBuilder =>
    {
        logBuilder.SetMinimumLevel(LogLevel.Information);
        logBuilder.AddConsole();
    });
    return StaticLogging.CreateLogger<Program>();
}
```

**Middleware pipeline order** (`Host/{App}.Api/WebApplicationBuilderExtensions.cs`):

```csharp
public static WebApplication ConfigurePipeline(this WebApplication app)
{
    app.UseExceptionHandler();     // 1. Catch unhandled exceptions first
    app.UseHttpsRedirection();     // 2. Redirect HTTP -> HTTPS
    app.UseRouting();              // 3. Route resolution
    app.UseRateLimiter();          // 4. Rate limiting before auth
    app.UseAuthentication();       // 5. Authenticate
    app.UseAuthorization();        // 6. Authorize

    // Dev-only: OpenAPI + Scalar UI
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("{App} API");
            options.WithTheme(ScalarTheme.Moon);
        });
    }

    // Health + liveness
    app.MapHealthChecks("/health");
    app.MapGet("/alive", () => Results.Ok("Alive"));

    // API endpoint groups
    var apiGroup = app.MapGroup("api");
    apiGroup.Map{Entity}Endpoints();

    return app;
}
```

---

## Request Context Resolution

**Source:** `Host/{App}.Bootstrapper/Registration/RegisterServices.RequestContext.cs`

Scoped `IRequestContext<string, Guid?>` factory: correlation ID from `X-Correlation-ID` header, claim precedence (`oid` > `NameIdentifier` > `sub`), tenant from `userTenantId` claim, role extraction, background service fallback.

```csharp
private static void AddRequestContextServices(IServiceCollection services)
{
    services.AddScoped<IRequestContext<string, Guid?>>(provider =>
    {
        var httpContext = provider.GetService<IHttpContextAccessor>()?.HttpContext;

        // Correlation ID: prefer header, fallback to new GUID
        var correlationId = Guid.NewGuid().ToString();
        if (httpContext != null)
        {
            var headers = httpContext.Request?.Headers;
            if (headers != null && headers.TryGetValue("X-Correlation-ID", out var headerValues))
            {
                var headerValue = headerValues.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(headerValue))
                    correlationId = headerValue;
            }
        }

        // Background service fallback -- no HttpContext available
        if (httpContext == null)
        {
            return new RequestContext<string, Guid?>(
                correlationId, $"BackgroundService-{correlationId}", null, []);
        }

        // Claim precedence for audit identity
        var user = httpContext.User;
        var auditId = user.Claims.FirstOrDefault(c => c.Type == "oid")?.Value
            ?? user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
            ?? user.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
            ?? "NoAuditClaim";

        // Tenant from custom claim
        var tenantIdClaim = user.Claims.FirstOrDefault(c => c.Type == "userTenantId")?.Value;
        var tenantId = Guid.TryParse(tenantIdClaim, out var tenantGuid)
            ? tenantGuid : (Guid?)null;

        // Roles
        var rolesList = user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value).ToList();

        return new RequestContext<string, Guid?>(correlationId, auditId, tenantId, rolesList);
    });
}
```

---

## Conditional Auth Configuration

**Source:** `Host/{App}.Api/RegisterApiServices.cs`

No-op auth path when config section is missing; full JwtBearer + MicrosoftIdentityWebApi + fallback policy when present.

```csharp
private static void AddAuthentication(IServiceCollection services, IConfiguration config, ILogger logger)
{
    string authConfigSectionName = "{App}Api_EntraID";
    var configSection = config.GetSection(authConfigSectionName);

    // No-op path: auth section absent -- register empty auth so middleware doesn't throw
    if (!configSection.GetChildren().Any())
    {
        logger.LogInformation(
            "No Auth Config ({ConfigSectionName}) Found; Auth will not be configured.",
            authConfigSectionName);
        services.AddAuthentication();
        services.AddAuthorization();
        return;
    }

    // Full auth: JwtBearer default scheme + Microsoft Identity Web
    logger.LogInformation("Configure auth - {ConfigSectionName}", authConfigSectionName);
    services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddMicrosoftIdentityWebApi(config.GetSection(authConfigSectionName));

    // Fallback policy: require authenticated user by default
    services.AddAuthorizationBuilder()
        .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build())
        .AddPolicy(AppConstants.ROLE_GLOBAL_ADMIN,
            policy => policy.RequireRole(AppConstants.ROLE_GLOBAL_ADMIN))
        .AddPolicy(AppConstants.ROLE_USER,
            policy => policy.RequireRole(AppConstants.ROLE_USER));
}
```
