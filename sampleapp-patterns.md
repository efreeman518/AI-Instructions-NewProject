# Sampleapp Pattern Catalog

> **Purpose:** Distilled code patterns from the TaskFlow reference solution (`sampleapp/`). These are the **composite/cross-cutting patterns** that templates alone cannot express — they span multiple files, coordinate across projects, or represent infrastructure that must exist before per-entity templates can work.
>
> **When to use:** Read the relevant section here instead of opening sampleapp `.cs` files. Only open the actual sampleapp source if this catalog is insufficient for your specific situation.
>
> **Token mapping:** `{Project}` = TaskFlow, `{App}` = TaskFlow, `{Host}` = TaskFlow, `{Entity}` = TodoItem

---

## 1. Self-Referencing Entity Hierarchy

An entity that relates to itself via a nullable FK. Requires coordinated changes in entity, EF config, and a domain rule.

**Entity (Domain.Model/Entities/TodoItem.cs):**
```csharp
public Guid? ParentId { get; private set; }
public TodoItem? Parent { get; private set; }
public ICollection<TodoItem> SubTasks { get; private set; } = [];
```

**EF Configuration:**
```csharp
builder.HasOne(e => e.Parent)
    .WithMany(e => e.SubTasks)
    .HasForeignKey(e => e.ParentId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(e => new { e.TenantId, e.ParentId })
    .HasDatabaseName("IX_TodoItem_TenantId_ParentId");
```

**Domain Rule (TodoItemHierarchyRule.cs):**
```csharp
public class TodoItemHierarchyRule
    : RuleBase<(Guid ItemId, Guid? ProposedParentId, IReadOnlyList<Guid> AncestorChain)>
{
    public const int MaxDepth = 5;
    public override bool IsSatisfiedBy(
        (Guid ItemId, Guid? ProposedParentId, IReadOnlyList<Guid> AncestorChain) input)
    {
        if (input.ProposedParentId == input.ItemId) return false;       // no self-parent
        if (input.AncestorChain.Contains(input.ItemId)) return false;   // no circular ref
        return input.AncestorChain.Count < MaxDepth;                    // depth limit
    }
}
```

---

## 2. Polymorphic Join (EntityType Discriminator)

Entity that attaches to multiple parent types via an enum discriminator instead of a typed FK. No DB-level FK constraint — validated at the application layer.

**Entity (Attachment.cs):**
```csharp
public Guid EntityId { get; init; }
public EntityType EntityType { get; init; }  // enum: TodoItem | Comment | Team
```

**EF Configuration:**
```csharp
builder.Property(e => e.EntityType)
    .HasConversion<string>()
    .HasMaxLength(50);

builder.HasIndex(e => new { e.EntityType, e.EntityId })
    .HasDatabaseName("IX_Attachment_EntityType_EntityId");
// No .HasForeignKey() — no DB constraint; app-layer validates entity existence
```

---

## 3. Many-to-Many via Explicit Join Entity

Junction entity with composite PK. Does **not** inherit `EntityBase` — no standalone `Id` column.

**Entity (TodoItemTag.cs):**
```csharp
public class TodoItemTag  // NOT : EntityBase
{
    public Guid TodoItemId { get; init; }
    public Guid TagId { get; init; }
    public DateTimeOffset AppliedAt { get; init; } = DateTimeOffset.UtcNow;

    public static TodoItemTag Create(Guid todoItemId, Guid tagId) =>
        new() { TodoItemId = todoItemId, TagId = tagId };
}
```

**EF Configuration (uses `IEntityTypeConfiguration<T>`, not `EntityBaseConfiguration<T>`):**
```csharp
builder.HasKey(e => new { e.TodoItemId, e.TagId }).IsClustered();

builder.HasOne<TodoItem>().WithMany(t => t.TodoItemTags)
    .HasForeignKey(e => e.TodoItemId).OnDelete(DeleteBehavior.Cascade);

builder.HasOne<Tag>().WithMany()
    .HasForeignKey(e => e.TagId).OnDelete(DeleteBehavior.Cascade);
```

---

## 4. Value Object / Owned Type

Record-based value object with no identity. Stored as columns on the parent table via `OwnsOne`.

**Value Object (DateRange.cs):**
```csharp
public record DateRange
{
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    private DateRange() { }

    public static DomainResult<DateRange> Create(DateTimeOffset? start, DateTimeOffset? due)
    {
        if (start.HasValue && due.HasValue && start.Value >= due.Value)
            return DomainResult<DateRange>.Failure("StartDate must be before DueDate.");
        return DomainResult<DateRange>.Success(new DateRange { StartDate = start, DueDate = due });
    }
}
```

**Parent EF Configuration:**
```csharp
builder.OwnsOne(e => e.DateRange, dr =>
{
    dr.Property(d => d.StartDate).HasColumnName("DateRange_StartDate");
    dr.Property(d => d.DueDate).HasColumnName("DateRange_DueDate");
});
```

---

## 5. Domain Rules / Specification Pattern

Infrastructure for composable business rules. Must exist before any concrete rule.

**Base types (Domain.Model/Rules/):**
```csharp
public interface IRule<in T>
{
    string ErrorMessage { get; }
    bool IsSatisfiedBy(T entity);
}

public abstract class RuleBase<T> : IRule<T>
{
    public abstract string ErrorMessage { get; }
    public abstract bool IsSatisfiedBy(T entity);
    public DomainResult<T> Evaluate(T entity) =>
        IsSatisfiedBy(entity)
            ? DomainResult<T>.Success(entity)
            : DomainResult<T>.Failure(ErrorMessage);
}

// Composites — AND collects all errors, OR short-circuits on first pass
public class AllRule<T>(IEnumerable<IRule<T>> rules) : IRule<T>
{
    public string ErrorMessage =>
        string.Join("; ", rules.Where(r => !r.IsSatisfiedBy(default!)).Select(r => r.ErrorMessage));
    public bool IsSatisfiedBy(T entity) => rules.All(r => r.IsSatisfiedBy(entity));
}

// Pipeline evaluator
public static class RuleEvaluator
{
    public static DomainResult<T> EvaluateAll<T>(T entity, params IRule<T>[] rules) =>
        rules.All(r => r.IsSatisfiedBy(entity))
            ? DomainResult<T>.Success(entity)
            : DomainResult<T>.Failure(rules.Where(r => !r.IsSatisfiedBy(entity))
                .Select(r => r.ErrorMessage).ToArray());
}
```

---

## 6. Aspire AppHost Wiring

System-level orchestration connecting all hosts to shared resources. Every new host/resource modifies this file.

**AppHost/AppHost.cs:**
```csharp
var password = builder.AddParameter("aspire-sql-password", secret: true);
var sqlServer = builder.AddSqlServer("sql", password, port: 38433)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("taskflow-sql-data");
var taskflowDb = sqlServer.AddDatabase("taskflowdb");
var redis = builder.AddRedis("redis");

var api = builder.AddProject<Projects.TaskFlow_Api>("taskflowapi")
    .WithReference(taskflowDb, connectionName: "TaskFlowDbContextTrxn")
    .WithReference(taskflowDb, connectionName: "TaskFlowDbContextQuery")
    .WithReference(redis, connectionName: "Redis1")
    .WaitFor(sqlServer).WaitFor(redis);

var scheduler = builder.AddProject<Projects.TaskFlow_Scheduler>("taskflowscheduler")
    .WithReference(taskflowDb, connectionName: "TaskFlowDbContextTrxn")
    .WithReference(taskflowDb, connectionName: "TaskFlowDbContextQuery")
    .WithReference(taskflowDb, connectionName: "SchedulerDbContext")
    .WaitFor(sqlServer).WithReplicas(1); // CRITICAL: TickerQ = single replica

var gateway = builder.AddProject<Projects.TaskFlow_Gateway>("taskflowgateway")
    .WithReference(api).WithReference(scheduler).WaitFor(api);

// Note: Projects.TaskFlow_Api uses underscores because C# identifiers can't contain dots
```

**Key rules:**
- Same database, different `connectionName` per DbContext
- `WaitFor()` for health-based startup ordering
- `WithReplicas(1)` for TickerQ (no concurrent scheduler instances)
- `WithDataVolume()` for persistent SQL data across restarts

---

## 7. Gateway Claims Transformation + Token Relay

Multi-file cross-cutting pattern: normalize external identity claims → forward to downstream API via YARP transforms.

**GatewayClaimsTransformer.cs (IClaimsTransformation):**
```csharp
public class GatewayClaimsTransformer : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        // B2C extension claims → standard claim types
        var roleClaims = identity!.FindAll("extension_Roles").ToList();
        foreach (var r in roleClaims)
            identity.AddClaim(new Claim(ClaimTypes.Role, r.Value));
        // Same for extension_Email → ClaimTypes.Email
        return Task.FromResult(principal);
    }
}
```

**YARP request transform (in Gateway RegisterServices):**
```csharp
context.AddRequestTransform(async transformContext =>
{
    // Package original user claims as JSON header for downstream API
    AddOriginalUserClaimsHeader(transformContext); // JWT → X-Orig-Request JSON

    // Acquire client-credential token for downstream service
    var token = await tokenService.GetAccessTokenAsync(clusterId);
    transformContext.ProxyRequest!.Headers.Authorization = new("Bearer", token);
});
```

**TokenService — cached client credentials:**
```csharp
private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expiry)> _cache = new();

public async Task<string> GetAccessTokenAsync(string clusterId)
{
    if (_cache.TryGetValue(clusterId, out var cached)
        && cached.Expiry > DateTimeOffset.UtcNow.AddMinutes(5))
        return cached.Token;
    // ... acquire new token via MSAL, cache with expiry
}
```

---

## 8. Multi-Tenant Query Filter (Automatic)

Reflection + expression trees in the abstract DbContext base. Auto-applies `HasQueryFilter` to every `ITenantEntity<Guid>` — no per-entity config needed.

**TaskFlowDbContextBase.cs:**
```csharp
private static void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (!typeof(ITenantEntity<Guid>).IsAssignableFrom(entityType.ClrType)) continue;
        if (entityType.IsOwned()) continue; // skip owned types

        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var tenantIdProperty = Expression.Property(parameter, "TenantId");

        modelBuilder.Entity(entityType.ClrType)
            .HasQueryFilter(
                Expression.Lambda(
                    Expression.NotEqual(tenantIdProperty,
                        Expression.Constant(Guid.Empty)),
                    parameter));
    }
}
```

Called from `OnModelCreating` — new tenant entities automatically get the filter by implementing `ITenantEntity<Guid>`.

---

## 9. FusionCache Named Cache Setup

Configuration-driven loop in the Bootstrapper. Supports multiple named caches with conditional Redis L2 + backplane.

**CacheSettings POCO:**
```csharp
public class CacheSettings
{
    public string Name { get; set; } = "Default";
    public int DurationMinutes { get; set; } = 30;
    public int DistributedCacheDurationMinutes { get; set; } = 60;
    public string? RedisConnectionStringName { get; set; } = "Redis1";
}
```

**DI Registration (Bootstrapper/RegisterServices.cs):**
```csharp
var cacheSettingsList = config.GetSection("CacheSettings").Get<List<CacheSettings>>() ?? [];
foreach (var settings in cacheSettingsList)
{
    var fusionBuilder = services.AddFusionCache(settings.Name)
        .WithSystemTextJsonSerializer(jsonOptions)
        .WithCacheKeyPrefix($"{settings.Name}:")
        .WithDefaultEntryOptions(new FusionCacheEntryOptions
        {
            Duration = TimeSpan.FromMinutes(settings.DurationMinutes),
            IsFailSafeEnabled = true,
            FactorySoftTimeout = TimeSpan.FromSeconds(5),
        });

    var redisConn = config.GetConnectionString(settings.RedisConnectionStringName ?? "Redis1");
    if (!string.IsNullOrWhiteSpace(redisConn))
    {
        fusionBuilder
            .WithDistributedCache(new RedisCache(
                Options.Create(new RedisCacheOptions { Configuration = redisConn })))
            .WithBackplane(new RedisBackplane(
                Options.Create(new RedisBackplaneOptions { Configuration = redisConn })));
    }
}
```

---

## 10. TickerQ Scheduler Configuration

Three-layer pattern: host-level `AddTickerQ` config → `BaseTickerQJob` infrastructure → thin adapter jobs.

**Host Registration:**
```csharp
services.AddTickerQ<TimeTickerEntity, CronTickerEntity>(options =>
{
    options.SetExceptionHandler<TaskFlowSchedulerExceptionHandler>();
    options.ConfigureScheduler(scheduler =>
    {
        scheduler.MaxConcurrency = Environment.ProcessorCount;
        scheduler.SchedulerTimeZone = TimeZoneInfo.Utc;
        scheduler.FallbackIntervalChecker = TimeSpan.FromSeconds(pollIntervalSeconds);
    });
    options.AddOperationalStore(efOptions =>
    {
        efOptions.UseTickerQDbContext<TickerQDbContext>(optionsBuilder =>
            optionsBuilder.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "Scheduler")),
            schema: "Scheduler");
    });
    options.AddDashboard(d => { d.SetBasePath("/scheduler"); d.WithBasicAuth(user, pass); });
});
```

**BaseTickerQJob — scoped resolution + telemetry:**
```csharp
public abstract class BaseTickerQJob(IServiceScopeFactory scopeFactory, ILogger logger)
{
    protected async Task ExecuteJobAsync<THandler>(
        string jobName, TickerFunctionContext ctx, CancellationToken ct)
        where THandler : class
    {
        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
        // ... execute with Activity tracing, error logging, metrics
    }
}
```

**Concrete Job — thin adapter with cron attribute:**
```csharp
[TickerFunction("ProcessDueReminders", "10 */5 * * * *", TickerTaskPriority.High)]
public async Task ProcessDueRemindersAsync(TickerFunctionContext ctx, CancellationToken ct)
{
    await ExecuteJobAsync<ProcessDueRemindersHandler>("ProcessDueReminders", ctx, ct);
}
```

---

## 11. Test Infrastructure

Foundation classes that must exist before per-entity test templates work.

**CustomApiFactory (Test.Integration):**
```csharp
public class CustomApiFactory<TProgram>(string? dbConnectionString = null)
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development")
            .ConfigureAppConfiguration((_, cfg) =>
                cfg.AddJsonFile("appsettings-test.json", optional: false))
            .ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>(); // kill background services in tests
                DbSupport.ConfigureServicesTestDB<TaskFlowDbContextTrxn, TaskFlowDbContextQuery>(
                    services, dbConnectionString, dbName);
            });
    }
}
```

**DbSupport — generic DbContext swap (Test.Support):**
```csharp
public static void ConfigureServicesTestDB<TTrxn, TQuery>(
    IServiceCollection services, string? dbConn, string dbName)
    where TTrxn : DbContext where TQuery : DbContext
{
    // Remove pooled factory registrations
    services.RemoveAll<DbContextOptions<TTrxn>>();
    services.RemoveAll<TTrxn>();
    services.RemoveAll<IDbContextFactory<TTrxn>>();
    // ... same for TQuery

    if (dbConn == "UseInMemoryDatabase")
    {
        services.AddDbContext<TTrxn>(opt =>
            opt.UseInMemoryDatabase(dbName), ServiceLifetime.Singleton, ServiceLifetime.Singleton);
        services.AddDbContext<TQuery>(opt =>
            opt.UseInMemoryDatabase(dbName), ServiceLifetime.Singleton, ServiceLifetime.Singleton);
    }
}
```

**IntegrationTestBase — real Bootstrapper + test identity:**
```csharp
ServicesCollection
    .RegisterInfrastructureServices(Config)
    .RegisterApplicationServices(Config);

// Override identity for test isolation
ServicesCollection.AddTransient<IRequestContext>(_ =>
    new RequestContext(correlationId, $"Test-{testName}-{correlationId}", tenantId: null, roles: []));
```

---

## 12. Background Service Channel Pattern

System-level producer/consumer with bounded channel. Any service can enqueue; single `BackgroundService` drains.

**Interface + BoundedChannel queue:**
```csharp
public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(
        Func<IServiceProvider, CancellationToken, Task> workItem);
    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(
        CancellationToken ct);
}

public class ChannelBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;

    public ChannelBackgroundTaskQueue(int capacity = 100)
    {
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }
}
```

**Consumer BackgroundService:**
```csharp
public class QueuedBackgroundService(
    IBackgroundTaskQueue taskQueue,
    IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await taskQueue.DequeueAsync(stoppingToken);
            using var scope = serviceScopeFactory.CreateScope();
            await workItem(scope.ServiceProvider, stoppingToken);
        }
    }
}
```

---

## 13. Dockerfile — Multi-Stage Chiseled

Each host needs its own Dockerfile listing its transitive `.csproj` dependencies for layer-cache optimization.

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config .
COPY Directory.Packages.props .
COPY global.json .
# Copy .csproj files first (layer caching — re-restore only when refs change)
COPY Domain/{App}.Domain.Model/{App}.Domain.Model.csproj Domain/{App}.Domain.Model/
COPY Application/{App}.Application.Contracts/{App}.Application.Contracts.csproj Application/{App}.Application.Contracts/
# ... every project in the dependency graph
RUN dotnet restore {Host}/{Host}.Api/{Host}.Api.csproj
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime — chiseled (no shell, no package manager, non-root)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "{Host}.Api.dll"]
```

**Key rules:** Chiseled image = minimal attack surface + non-root. Port 8080 (not 80). COPY-layer ordering matters — `.csproj` files first, source second.

---

## 14. Split DbContext (Query vs Trxn)

Three-class hierarchy: abstract base (all config) → Trxn (change-tracked, audit) → Query (NoTracking, read replica).

**Abstract Base (all DbSets and OnModelCreating live here):**
```csharp
public abstract class TaskFlowDbContextBase(DbContextOptions options)
    : DbContextBase<string, Guid?>(options)
{
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    // ... all entity DbSets

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContextBase).Assembly);
        ConfigureTenantQueryFilters(modelBuilder); // Pattern #8
    }
}
```

**Trxn (write — with audit interceptor):**
```csharp
public class TaskFlowDbContextTrxn(DbContextOptions<TaskFlowDbContextTrxn> options)
    : TaskFlowDbContextBase(options) { }
```

**Query (read — NoTracking, optional read replica):**
```csharp
public class TaskFlowDbContextQuery(DbContextOptions<TaskFlowDbContextQuery> options)
    : TaskFlowDbContextBase(options) { }
```

**Bootstrapper Registration:**
```csharp
// Write context — pooled, with audit interceptor
services.AddPooledDbContextFactory<TaskFlowDbContextTrxn>((sp, opt) =>
{
    ConfigureSqlOptions(opt, trxnConnectionString, isAzure);
    opt.AddInterceptors(sp.GetRequiredService<AuditInterceptor<string, Guid?>>());
});

// Read context — pooled, NoTracking, optional ApplicationIntent=ReadOnly
services.AddPooledDbContextFactory<TaskFlowDbContextQuery>((sp, opt) =>
{
    opt.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    ConfigureSqlOptions(opt, queryConnectionString, isAzure);
});
```

---

## 15. Internal Message Bus + Event Pipeline

Event records → handler registration → auto-discovery at startup. Spans 4 files across 3 projects.

**Event Records (Application.Contracts/Events/):**
```csharp
public record TodoItemCreatedEvent(Guid TodoItemId, Guid TenantId, string Title);
public record TodoItemUpdatedEvent(Guid TodoItemId, TodoItemStatus PreviousStatus, TodoItemStatus NewStatus);
public record TodoItemAssignedEvent(Guid TodoItemId, Guid NewAssignedToId, string AssignedBy);
```

**Handler DI Registration (Bootstrapper):**
```csharp
services.AddScoped<IMessageHandler<TodoItemCreatedEvent>, TodoItemCreatedEventHandler>();
services.AddScoped<IMessageHandler<TodoItemUpdatedEvent>, TodoItemUpdatedEventHandler>();
services.AddScoped<IMessageHandler<TodoItemAssignedEvent>, TodoItemAssignedEventHandler>();
```

**Auto-Registration at Startup (Bootstrapper/IHostExtensions.cs):**
```csharp
public static async Task RunStartupTasks(this IHost host)
{
    var msgBus = host.Services.GetRequiredService<IInternalMessageBus>();
    msgBus.AutoRegisterHandlers(); // scans DI for all IMessageHandler<T> implementations
    // ... then runs IStartupTask sequence
}
```

**Service publishes:**
```csharp
await _messageBus.PublishAsync(new TodoItemCreatedEvent(entity.Id, entity.TenantId, entity.Title));
```

---

## 16. Uno Platform — App Composition Root

Single file that wires auth, HTTP client, all services, and the full navigation tree.

**App.xaml.host.cs:**
```csharp
builder.Configure(host => host
    .UseAuthentication(auth => auth
        .AddCustom(custom =>
        {
            custom.Login(async (sp, dis, creds, ct) => await ProcessCredentials(creds));
        }, name: "CustomAuth"))

    .UseHttp((ctx, services) =>
    {
        services.AddTransient<MockHttpMessageHandler>();
        services.AddKiotaClient<TaskFlowApiClient>(ctx,
            options: new EndpointOptions { Url = ctx.Configuration["GatewayBaseUrl"] },
            configure: (cb, ep) =>
            {
                if (ctx.Configuration.GetValue<bool>("Features:UseMocks"))
                    cb.ConfigurePrimaryAndInnerHttpMessageHandler<MockHttpMessageHandler>();
            });
    })

    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<ITodoItemService, TodoItemService>()
                .AddSingleton<ICategoryService, CategoryService>();
        // Add new entity services here
    })

    .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes));

// Navigation — ViewMap for each page, nested RouteMap for hierarchy
private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
{
    views.Register(
        new ViewMap<MainPage, MainModel>(),
        new DataViewMap<TodoItemDetailPage, TodoItemDetailModel, TodoItem>(),
        new ViewMap<CreateTodoItemPage, CreateTodoItemModel>(),
        // Add new page registrations here
    );

    routes.Register(new RouteMap("", Nested: new[]
    {
        new RouteMap("Main", Nested: new[]
        {
            new RouteMap("TodoItemList", IsDefault: true),
            new RouteMap("TodoItemDetail"),
            new RouteMap("CreateTodoItem"),
            // Add new routes here
        }),
    }));
}
```

**Key rules:** Every new page needs (1) a `ViewMap`/`DataViewMap` entry and (2) a `RouteMap` entry. Kiota client uses `GatewayBaseUrl` from config (points to YARP Gateway, never direct API). Mock handler is swappable via feature flag.
