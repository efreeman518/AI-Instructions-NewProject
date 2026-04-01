# Sampleapp Pattern Catalog

Distilled cross-cutting patterns that span multiple files/projects. This file is **self-contained** -- all composition wiring snippets are embedded directly so no external sample-app source is required.

## How to Use This File

- Load this file first when building a new slice or optional host.
- Pick the matching pattern and then open only the referenced skill/template.
- Avoid duplicating scaffolding logic already defined in templates.

---

## Cross-Cutting Pattern Map

| Pattern | When | Use | Primary References |
|---|---|---|---|
| Self-referencing hierarchy | Parent/child trees (e.g., subtasks) | Align entity nav props + EF config + depth/cycle rule | [../templates/entity-template.md](../templates/entity-template.md), [../templates/ef-configuration-template.md](../templates/ef-configuration-template.md), [../templates/domain-rules-template.md](../templates/domain-rules-template.md) |
| Polymorphic join discriminator | One attachment/comment type used by many aggregates | `{EntityType, EntityId}` + indexed discriminator | [../skills/data-persistence.md](../skills/data-persistence.md), [../templates/ef-configuration-template.md](../templates/ef-configuration-template.md) |
| Explicit many-to-many join | Join metadata needed (`AppliedAt`, audit, flags) | Explicit join entity + composite key | [../skills/data-persistence.md](../skills/data-persistence.md), [../templates/entity-template.md](../templates/entity-template.md) |
| Value object / owned type | Value semantics under aggregate root | Record + `Create()` + `OwnsOne` mapping | [../templates/domain-rules-template.md](../templates/domain-rules-template.md), [../templates/ef-configuration-template.md](../templates/ef-configuration-template.md) |
| Domain rules/specification | Invariant validation across inputs | `RuleBase<T>` + composed rules + `DomainResult` | [../templates/domain-rules-template.md](../templates/domain-rules-template.md) |
| Split query/trxn DbContext | Read/write concerns differ | Shared base model + Trxn/Query contexts + query no-tracking | [../skills/data-persistence.md](../skills/data-persistence.md), [../templates/repository-template.md](../templates/repository-template.md) |
| Multi-tenant query filter | Tenant-safe reads by default | Global filter on `ITenantEntity<Guid>` in DbContext base | [../skills/multi-tenant.md](../skills/multi-tenant.md), [../skills/data-persistence.md](../skills/data-persistence.md) |
| Internal event pipeline | Domain/service events trigger side effects | Contracts + handler DI + startup auto-registration | [../skills/messaging.md](../skills/messaging.md), [../templates/message-handler-template.md](../templates/message-handler-template.md) |
| Gateway token relay | API gateway forwards identity to downstream services | Claims normalization + request transform + service token acquisition | [../skills/gateway.md](../skills/gateway.md), [../skills/identity-management.md](../skills/identity-management.md) |
| FusionCache named caches | Per-feature cache policies + Redis backplane | Configure caches from settings loop | [../skills/caching.md](../skills/caching.md), [../skills/configuration-secrets.md](../skills/configuration-secrets.md) |
| TickerQ scheduler host | Cron/time orchestration in separate host | Thin `[TickerFunction]` adapters + handler classes + single replica | [../skills/background-services.md](../skills/background-services.md), [../skills/aspire.md](../skills/aspire.md) |
| Channel background queue | In-process async work dispatch | Bounded channel + scoped consumer service | [../skills/background-services.md](../skills/background-services.md) |
| Mixed-store reconciliation | Feature spans SQL + document/stream/table stores | Authoritative boundary + reconciliation handler/job + replay-safe correction flow | [vertical-slice-checklist.md](vertical-slice-checklist.md), [../skills/messaging.md](../skills/messaging.md), [../skills/background-services.md](../skills/background-services.md) |
| Timeline projection | Support/dispute critical workflows | Append-only event log + query read model + timeline endpoint | [../skills/messaging.md](../skills/messaging.md), [vertical-slice-checklist.md](vertical-slice-checklist.md) |
| Aspire AppHost composition | Multi-host local orchestration | Add resources once, wire references per host, apply `WaitFor` | [../skills/aspire.md](../skills/aspire.md), [../skills/solution-structure.md](../skills/solution-structure.md) |
| Docker multi-stage chiseled | Smaller runtime images + cached restore layers | Copy project files first, then publish to chiseled runtime | [../skills/cicd.md](../skills/cicd.md), [../skills/package-dependencies.md](../skills/package-dependencies.md) |
| Result-through-layers | All CRUD flows -- domain to endpoint | `DomainResult<T>` in entities/services, `Result.Match()` in endpoints; never throw for business errors | [../skills/domain-model.md](../skills/domain-model.md), [../templates/service-template.md](../templates/service-template.md), [../templates/endpoint-template.md](../templates/endpoint-template.md), [../skills/api.md](../skills/api.md) |
| DefaultExceptionHandler | Unexpected/infrastructure exceptions only | `IExceptionHandler` maps exception types to `ProblemDetails`; last-resort safety net, not a control-flow mechanism | [../skills/api.md](../skills/api.md) |
| Uno composition root | Client DI/auth/navigation bootstrap | Configure auth + HTTP/Kiota + route maps centrally | [../skills/uno-ui.md](../skills/uno-ui.md), [../templates/mvux-model-template.md](../templates/mvux-model-template.md) |

---

## Canonical Composite Snippets

Quick-reference for patterns that span multiple files. These are excerpts only -- the owning template or skill file is the authoritative source for full implementation.

### 1) Self-Referencing Hierarchy

Key wiring points: nav props on entity + `HasOne/WithMany/HasForeignKey/OnDelete(Restrict)` in EF config + domain rule to prevent self-parenting/cycles. See [../templates/entity-template.md](../templates/entity-template.md) and [../templates/ef-configuration-template.md](../templates/ef-configuration-template.md).

### 2) TickerQ Job Adapter + Handler Split

Scheduler method is a thin adapter (`[TickerFunction]` -> `ExecuteJobAsync<THandler>`). All business logic stays in the `IScheduledJobHandler` implementation. See [../skills/background-services.md](../skills/background-services.md).

### 3) Gateway Claim Relay

Normalize inbound claims, then forward a service token. Pattern: `AddRequestTransform` -> `AddOriginalUserClaimsHeader` -> `GetAccessTokenAsync` -> set `Authorization` header. See [../skills/gateway.md](../skills/gateway.md) and [../skills/identity-management.md](../skills/identity-management.md).

### 4) Result-Through-Layers Error Strategy

Two complementary error paths -- never mix them:

```
[Domain]   DomainResult<T>.Success / .Failure  -- business validation, rules, state transitions
               |
[Service]  Result<T>.Success / .Failure / .None -- orchestration, tenant boundary, structure validation
               |
[Endpoint] result.Match(ok, errors -> ProblemDetails, notFound -> NotFound)
               |
[DefaultExceptionHandler]                       -- catches ONLY unexpected exceptions; last-resort safety net
```

**Rule:** Use `Result`/`DomainResult` for all expected outcomes. Throw only for truly unexpected failures. See [../skills/domain-model.md](../skills/domain-model.md), [../templates/service-template.md](../templates/service-template.md), [../templates/endpoint-template.md](../templates/endpoint-template.md).

---

## Composition Wiring Patterns

These patterns show how individual components wire together across files. They are the hardest patterns for AI to infer from templates alone.

### 5) API Startup Sequence

**Source:** `{App}.Api/Program.cs` (TaskFlow.Api/Program.cs)

The startup follows a strict order: early logger, service registration chain, build, pipeline, startup tasks, run. The entire body is wrapped in try/catch/finally using a `StaticLogging` logger created before the host exists.

```csharp
var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;
var appName = config.GetValue<string>("AppName") ?? "{App}.Api"; // "TaskFlow.Api"
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

**Middleware pipeline order** (`{App}.Api/WebApplicationBuilderExtensions.cs`):

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
            options.WithTitle("{App} API"); // "TaskFlow API"
            options.WithTheme(ScalarTheme.Moon);
        });
    }

    // Health + liveness
    app.MapHealthChecks("/health");
    app.MapGet("/alive", () => Results.Ok("Alive"));

    // API endpoint groups
    var apiGroup = app.MapGroup("api");
    apiGroup.Map{Entity}Endpoints(); // e.g., apiGroup.MapTodoItemEndpoints();

    return app;
}
```

### 6) Database Context Pooling & Scoped Wrappers

**Source:** `{App}.Bootstrapper/Registration/RegisterServices.Database.cs` (TaskFlow.Bootstrapper)

Dual-context registration: pooled factories for Trxn and Query contexts, `DbContextScopedFactory` wrappers for scoped resolution, audit interceptor on Trxn only, `ConnectionNoLockInterceptor` on both, Azure vs local SQL detection, `ReadOnly` intent injection for Query.

```csharp
private static void AddDatabaseServices(IServiceCollection services, IConfiguration config)
{
    // Repository registrations (scoped, per-entity, Trxn + Query pairs)
    services.AddScoped<I{Entity}RepositoryQuery, {Entity}RepositoryQuery>();
    services.AddScoped<I{Entity}RepositoryTrxn, {Entity}RepositoryTrxn>();
    // ... additional repositories

    // Interceptors
    services.AddTransient<AuditInterceptor<string, Guid?>>();
    services.AddTransient<ConnectionNoLockInterceptor>();

    ConfigureDatabaseContexts(services, config);
}
```

**Dual pooled context wiring:**

```csharp
private static void ConfigureSqlDatabase(IServiceCollection services,
    string dbConnectionStringTrxn, string dbConnectionStringQuery)
{
    // -- TRXN context: audit interceptor + exception processor
    services.AddPooledDbContextFactory<{App}DbContextTrxn>((sp, options) =>
    {
        ConfigureTrxnDbContext(options, dbConnectionStringTrxn);
        var auditInterceptor = sp.GetRequiredService<AuditInterceptor<string, Guid?>>();
        options.UseExceptionProcessor().AddInterceptors(auditInterceptor);
    });
    services.AddScoped<DbContextScopedFactory<{App}DbContextTrxn, string, Guid?>>();
    services.AddScoped(sp => sp.GetRequiredService<DbContextScopedFactory<{App}DbContextTrxn, string, Guid?>>()
        .CreateDbContext());

    // -- QUERY context: no audit interceptor, no-tracking, ReadOnly intent
    services.AddPooledDbContextFactory<{App}DbContextQuery>((sp, options) =>
    {
        ConfigureQueryDbContext(options, dbConnectionStringQuery);
        options.UseExceptionProcessor();
    });
    services.AddScoped<DbContextScopedFactory<{App}DbContextQuery, string, Guid?>>();
    services.AddScoped(sp => sp.GetRequiredService<DbContextScopedFactory<{App}DbContextQuery, string, Guid?>>()
        .CreateDbContext());
}
```

**Azure vs local detection + ReadOnly intent for Query:**

```csharp
private static void ConfigureSqlOptions(DbContextOptionsBuilder options, string connectionString)
{
    if (connectionString.Contains("database.windows.net"))
    {
        options.UseAzureSql(connectionString, sqlOptions =>
        {
            sqlOptions.UseCompatibilityLevel(170);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        });
    }
    else
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.UseCompatibilityLevel(160);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        });
    }
}

private static void ConfigureQueryDbContext(DbContextOptionsBuilder options, string connectionString)
{
    var readOnlyConnectionString = connectionString.Contains("ApplicationIntent=")
        ? connectionString
        : connectionString + ";ApplicationIntent=ReadOnly";
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    ConfigureSqlOptions(options, readOnlyConnectionString);
}
```

### 7) DbContext OnModelCreating Order

**Source:** `Infrastructure.Data/{App}DbContextBase.cs` (TaskFlowDbContextBase)

The base context inherits from `DbContextBase<string, Guid?>` (shared library). `OnModelCreating` must follow this exact call order:

```csharp
public abstract class {App}DbContextBase(DbContextOptions options)
    : DbContextBase<string, Guid?>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);                                      // 1. Base class config

        modelBuilder.HasDefaultSchema("{schemaName}");                           // 2. Default schema ("taskflow")

        modelBuilder.ApplyConfigurationsFromAssembly(                            // 3. All IEntityTypeConfiguration<T>
            typeof({App}DbContextBase).Assembly);

        ConfigureDefaultDataTypes(modelBuilder);                                 // 4. Global type defaults
        SetTableNames(modelBuilder);                                             // 5. Table naming convention
        ConfigureTenantQueryFilters(modelBuilder);                               // 6. Tenant filters
    }
```

**Dynamic tenant query filter** -- applied to every entity implementing `ITenantEntity<Guid>`:

```csharp
    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var tenantEntityClrTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantEntity<Guid>).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType);

        foreach (var clrType in tenantEntityClrTypes)
        {
            var filter = BuildTenantFilter(clrType);   // from DbContextBase -- uses IRequestContext.TenantId
            modelBuilder.Entity(clrType).HasQueryFilter(filter);
        }
    }
```

**Global decimal and datetime2 defaults** -- catches any property without an explicit column type:

```csharp
    private static void ConfigureDefaultDataTypes(ModelBuilder modelBuilder)
    {
        // All decimal/decimal? properties default to decimal(10,4)
        var decimalProperties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
            .Where(p => p.GetColumnType() == null);
        foreach (var property in decimalProperties)
            property.SetColumnType("decimal(10,4)");

        // All DateTime/DateTime? properties default to datetime2
        var dateProperties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .Where(p => p.GetColumnType() == null);
        foreach (var property in dateProperties)
            property.SetColumnType("datetime2");
    }
```

**Table naming** -- skips owned types, falls back to `DisplayName()`:

```csharp
    private static void SetTableNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.IsOwned()) continue;
            var current = entity.GetTableName();
            if (string.IsNullOrWhiteSpace(current))
                entity.SetTableName(entity.DisplayName());
        }
    }
}
```

### 8) Request Context Resolution

**Source:** `{App}.Bootstrapper/Registration/RegisterServices.RequestContext.cs` (TaskFlow.Bootstrapper)

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
        var auditId = user.Claims.FirstOrDefault(c => c.Type == "oid")?.Value        // Azure AD object ID
            ?? user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
            ?? user.Claims.FirstOrDefault(c => c.Type == "sub")?.Value                // OIDC subject
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

### 9) Multi-Cache Configuration

**Source:** `{App}.Bootstrapper/Registration/RegisterServices.Caching.cs` (TaskFlow.Bootstrapper)

FusionCache config loop: bind `CacheSettings[]` from configuration, register each as a named FusionCache with exact entry option defaults (fail-safe, jitter, eager refresh threshold), conditional Redis backplane per cache instance.

```csharp
private static void AddCachingServices(IServiceCollection services, IConfiguration config)
{
    List<CacheSettings> cacheSettings = [];
    config.GetSection("CacheSettings").Bind(cacheSettings);
    foreach (var cacheSettingsInstance in cacheSettings)
    {
        ConfigureFusionCacheInstance(services, config, cacheSettingsInstance);
    }
}

private static void ConfigureFusionCacheInstance(IServiceCollection services,
    IConfiguration config, CacheSettings cacheSettingsInstance)
{
    var jsonOptions = new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.Preserve,
    };

    var fcBuilder = services.AddFusionCache(cacheSettingsInstance.Name)
        .WithSystemTextJsonSerializer(jsonOptions)
        .WithCacheKeyPrefix($"{cacheSettingsInstance.Name}:")
        .WithDefaultEntryOptions(new FusionCacheEntryOptions()
        {
            Duration = TimeSpan.FromMinutes(cacheSettingsInstance.DurationMinutes),
            DistributedCacheDuration = TimeSpan.FromMinutes(
                cacheSettingsInstance.DistributedCacheDurationMinutes),
            IsFailSafeEnabled = true,
            FailSafeMaxDuration = TimeSpan.FromMinutes(
                cacheSettingsInstance.FailSafeMaxDurationMinutes),
            FailSafeThrottleDuration = TimeSpan.FromSeconds(
                cacheSettingsInstance.FailSafeThrottleDurationMinutes),
            JitterMaxDuration = TimeSpan.FromSeconds(10),
            FactorySoftTimeout = TimeSpan.FromSeconds(1),
            FactoryHardTimeout = TimeSpan.FromSeconds(30),
            EagerRefreshThreshold = 0.9f
        });

    ConfigureFusionCacheRedis(fcBuilder, config, cacheSettingsInstance);
}
```

**Conditional Redis backplane** -- only wired when `RedisConnectionStringName` is set in that cache's config:

```csharp
private static void ConfigureFusionCacheRedis(IFusionCacheBuilder fcBuilder,
    IConfiguration config, CacheSettings cacheSettingsInstance)
{
    if (!string.IsNullOrEmpty(cacheSettingsInstance.RedisConnectionStringName))
    {
        var redisConnectionString = config.GetConnectionString(
            cacheSettingsInstance.RedisConnectionStringName);
        fcBuilder
            .WithDistributedCache(new RedisCache(new RedisCacheOptions()
            {
                Configuration = redisConnectionString
            }))
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
            {
                Configuration = redisConnectionString
            }));
    }
}
```

### 10) Conditional Auth Configuration

**Source:** `{App}.Api/RegisterApiServices.cs` (TaskFlow.Api)

No-op auth path when config section is missing; full JwtBearer + MicrosoftIdentityWebApi + fallback policy when present.

```csharp
private static void AddAuthentication(IServiceCollection services, IConfiguration config, ILogger logger)
{
    string authConfigSectionName = "{App}Api_EntraID"; // "TaskFlowApi_EntraID"
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

### 11) Aspire Resource Wiring

**Source:** `Aspire/AppHost/AppHost.cs`

SQL with password parameter + data volume, Redis with data volume, per-service endpoints/references/WaitFor, Gateway wired to API only, Scheduler pinned to 1 replica.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// -- Shared infrastructure
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume("{app}-sql-data")        // "taskflow-sql-data"
    .AddDatabase("{App}Db");                 // "TaskFlowDb"

var redis = builder.AddRedis("redis")
    .WithDataVolume("{app}-redis-data");     // "taskflow-redis-data"

// -- API: references both SQL + Redis
var {app}Api = builder.AddProject<Projects.{App}_Api>("{app}api")  // "taskflowapi"
    .WithReference(sql)
    .WithReference(redis)
    .WithHttpEndpoint(port: 5065, name: "http-api")
    .WithHttpsEndpoint(port: 7065, name: "https-api")
    .WaitFor(sql)
    .WaitFor(redis);

// -- Scheduler: same infra refs, single replica
var {app}Scheduler = builder.AddProject<Projects.{App}_Scheduler>("{app}scheduler")
    .WithReference(sql)
    .WithReference(redis)
    .WithHttpEndpoint(port: 5100, name: "http-scheduler")
    .WithHttpsEndpoint(port: 7100, name: "https-scheduler")
    .WithReplicas(1)
    .WaitFor(sql)
    .WaitFor(redis);

// -- Gateway (YARP): references API only, not infra directly
var {app}Gateway = builder.AddProject<Projects.{App}_Gateway>("{app}gateway")
    .WithReference({app}Api)
    .WithHttpEndpoint(port: 5028, name: "http-gateway")
    .WithHttpsEndpoint(port: 7028, name: "https-gateway")
    .WaitFor({app}Api);

// -- Function App: SQL only
var functionApp = builder.AddProject<Projects.FunctionApp>("functionapp")
    .WaitFor(sql);

builder.Build().Run();
```

---

## Pattern Selection Rules

1. Prefer template-owned implementation details over catalog duplication.
2. Use this file for orchestration decisions across projects, not per-file boilerplate.
3. If a pattern touches 3+ projects, treat it as a composite and verify all references.
4. When uncertain, default to the simplest pattern that preserves clean architecture boundaries.

---

## Expected Output File Index

Expected file layout when scaffolding is complete. All paths relative to project root `src/`.

### Domain Layer
| Artifact | Path |
|---|---|
| Entity (root) | `Domain/Domain.Model/TodoItem.cs` |
| Entity (child) | `Domain/Domain.Model/Comment.cs` |
| Value object | `Domain/Domain.Model/DateRange.cs` |

### Data Access
| Artifact | Path |
|---|---|
| EF config (entity) | `Infrastructure/Infrastructure.Data/Configurations/TodoItemConfiguration.cs` |
| Write repository | `Infrastructure/Infrastructure.Repositories/TodoItemRepositoryTrxn.cs` |
| Read repository | `Infrastructure/Infrastructure.Repositories/TodoItemRepositoryQuery.cs` |
| Trxn DbContext | `Infrastructure/Infrastructure.Data/TaskFlowDbContextTrxn.cs` |
| Query DbContext | `Infrastructure/Infrastructure.Data/TaskFlowDbContextQuery.cs` |
| Updater | `Infrastructure/Infrastructure.Repositories/TodoItemUpdater.cs` |

### Application Layer
| Artifact | Path |
|---|---|
| Service | `Application/Application.Services/TodoItemService.cs` |
| DTO | `Application/Application.Models/TodoItemDto.cs` |
| Search filter | `Application/Application.Models/TodoItemSearchFilter.cs` |
| Mapper | `Application/Application.Mappers/TodoItemMapper.cs` |
| Contracts | `Application/Application.Contracts/` |
| Message handler | `Application/Application.MessageHandlers/TodoItemCreatedEventHandler.cs` |

### API Host
| Artifact | Path |
|---|---|
| Program.cs | `TaskFlow/TaskFlow.Api/Program.cs` |
| Endpoints | `TaskFlow/TaskFlow.Api/Endpoints/TodoItemEndpoints.cs` |
| RegisterApiServices | `TaskFlow/TaskFlow.Api/RegisterApiServices.cs` |
| Bootstrapper | `TaskFlow/TaskFlow.Bootstrapper/RegisterServices.cs` |

### Testing
| Artifact | Path |
|---|---|
| Unit (domain) | `Test/Test.Unit/Domain/TodoItemTests.cs` |
| Unit (mapper) | `Test/Test.Unit/Application/TodoItemMapperTests.cs` |
| Integration | `Test/Test.Integration/EndpointContractTests.cs` |
| Architecture | `Test/Test.Architecture/LayerDependencyTests.cs` |
| Test support | `Test/Test.Support/UnitTestBase.cs`, `InMemoryDbBuilder.cs`, `DbSupport.cs` |
| Endpoint tests | `Test/Test.Endpoints/Endpoints/CategoryEndpointsTests.cs` |
| Custom factory | `Test/Test.Integration/CustomApiFactory.cs` |

### Aspire
| Artifact | Path |
|---|---|
| AppHost | `Aspire/AppHost/AppHost.cs` |
| Service defaults | `Aspire/ServiceDefaults/` |

## Verification Checklist

Use this checklist after selecting patterns from the [Cross-Cutting Pattern Map](#cross-cutting-pattern-map) above:

- [ ] Selected patterns map to enabled workloads only
- [ ] Each chosen pattern has an explicit primary reference loaded
- [ ] No duplicate implementation guidance copied from templates
- [ ] Cross-project wiring (AppHost/Gateway/Scheduler/UI) is internally consistent
