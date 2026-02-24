// ═══════════════════════════════════════════════════════════════
// Pattern: Bootstrapper — centralized DI registration hub.
// Shared across ALL hosts (API, Scheduler, Functions, Tests).
// Contains NO host-specific concerns (no endpoints, no triggers, no YARP).
// Extension methods on IServiceCollection for fluent chaining.
// ═══════════════════════════════════════════════════════════════

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Contracts.Events;
using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.MessageHandlers;
using Application.Services;
using Infrastructure;
using Infrastructure.Notification;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EF.Common;
using EF.BackgroundServices.InternalMessageBus;
using EF.Data;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace TaskFlow.Bootstrapper;

/// <summary>
/// Pattern: Static class with extension methods organized by architectural layer.
/// Each Register*() method returns IServiceCollection for fluent chaining.
/// Private helper methods group related registrations for readability.
/// </summary>
public static class RegisterServices
{
    // ═══════════════════════════════════════════════════════════════
    // Domain Layer
    // Pattern: Mostly empty — domain logic lives in entities, not services.
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection RegisterDomainServices(
        this IServiceCollection services, IConfiguration config)
    {
        // Domain logic is encapsulated in entity methods and domain rules.
        // Nothing to register here for most domains.
        return services;
    }

    // ═══════════════════════════════════════════════════════════════
    // Application Layer
    // Pattern: Services + MessageHandlers.
    // Services are scoped (one per request), handlers are scoped (resolved per event).
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection RegisterApplicationServices(
        this IServiceCollection services, IConfiguration config)
    {
        AddApplicationServices(services, config);
        AddMessageHandlers(services);
        return services;
    }

    private static void AddApplicationServices(IServiceCollection services, IConfiguration config)
    {
        // Pattern: Per-service settings — bind from appsettings config sections.
        services.Configure<TodoItemServiceSettings>(config.GetSection(TodoItemServiceSettings.ConfigSectionName));
        services.Configure<TeamServiceSettings>(config.GetSection(TeamServiceSettings.ConfigSectionName));

        // Pattern: Cross-cutting application services — tenant validation, etc.
        services.AddScoped<ITenantBoundaryValidator, TenantBoundaryValidator>();

        // Pattern: One service per entity/aggregate — scoped lifetime aligns with request scope.
        services.AddScoped<ITodoItemService, TodoItemService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
    }

    private static void AddMessageHandlers(IServiceCollection services)
    {
        // Pattern: MessageHandlers — scoped so they can access scoped repos/services.
        // AutoRegisterHandlers() in IHostExtensions discovers these at startup.
        services.AddScoped<IMessageHandler<TodoItemCreatedEvent>, TodoItemCreatedEventHandler>();
        services.AddScoped<IMessageHandler<TodoItemUpdatedEvent>, TodoItemUpdatedEventHandler>();
        services.AddScoped<IMessageHandler<TodoItemAssignedEvent>, TodoItemAssignedEventHandler>();
    }

    // ═══════════════════════════════════════════════════════════════
    // Infrastructure Layer
    // Pattern: Database, caching, request context, Azure clients, startup tasks.
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection RegisterInfrastructureServices(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        AddRequestContextServices(services);
        AddDatabaseServices(services, config);
        AddCachingServices(services, config);
        AddNotificationServices(services, config);
        AddStartupTasks(services);
        return services;
    }

    // ═══════════════════════════════════════════════════════════════
    // Background Services
    // Pattern: Channel-based background task queue for fire-and-forget work.
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection RegisterBackgroundServices(
        this IServiceCollection services, IConfiguration config)
    {
        // Pattern: Channel task queue — registered in BackgroundServices project.
        // services.AddChannelBackgroundTaskQueue();
        return services;
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Helpers — Request Context
    // Pattern: Extracts identity, tenant, correlation from HttpContext or background context.
    // ═══════════════════════════════════════════════════════════════

    private static void AddRequestContextServices(IServiceCollection services)
    {
        // Pattern: Scoped IRequestContext — resolved differently for HTTP vs background contexts.
        services.AddScoped<IRequestContext<string, Guid?>>(provider =>
        {
            var httpContext = provider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
            var correlationId = Guid.NewGuid().ToString();

            if (httpContext != null)
            {
                // Pattern: Extract correlation ID from request header (set by Gateway or client).
                if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValues))
                {
                    var val = headerValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(val)) correlationId = val;
                }

                var user = httpContext.User;

                // Pattern: AuditId from JWT claims — "oid" (Entra ID object) or NameIdentifier fallback.
                var auditId = user.Claims.FirstOrDefault(c => c.Type == "oid")?.Value
                    ?? user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                    ?? "NoAuditClaim";

                // Pattern: TenantId from custom claim — set by Gateway's claims transformer.
                var tenantIdClaim = user.Claims.FirstOrDefault(c => c.Type == "userTenantId")?.Value;
                var tenantId = Guid.TryParse(tenantIdClaim, out var tid) ? tid : (Guid?)null;

                var roles = user.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                return new RequestContext<string, Guid?>(correlationId, auditId, tenantId, roles);
            }

            // Pattern: Background service context — no HTTP, no tenant, no roles.
            return new RequestContext<string, Guid?>(
                correlationId, $"BackgroundService-{correlationId}", null, []);
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Helpers — Database
    // Pattern: Pooled DbContext factories with scoped wrappers.
    // Trxn context = writes (with audit interceptor).
    // Query context = reads (NoTracking, ReadOnly intent).
    // ═══════════════════════════════════════════════════════════════

    private static void AddDatabaseServices(IServiceCollection services, IConfiguration config)
    {
        // Pattern: Repository registrations — one per entity, Trxn + Query pair.
        services.AddScoped<IGenericRepositoryTrxn, GenericRepositoryTrxn>();
        services.AddScoped<IGenericRepositoryQuery, GenericRepositoryQuery>();

        services.AddScoped<ITodoItemRepositoryTrxn, TodoItemRepositoryTrxn>();
        services.AddScoped<ITodoItemRepositoryQuery, TodoItemRepositoryQuery>();
        services.AddScoped<ICategoryRepositoryTrxn, CategoryRepositoryTrxn>();
        services.AddScoped<ICategoryRepositoryQuery, CategoryRepositoryQuery>();
        services.AddScoped<ITagRepositoryTrxn, TagRepositoryTrxn>();
        services.AddScoped<ITagRepositoryQuery, TagRepositoryQuery>();
        services.AddScoped<ITeamRepositoryTrxn, TeamRepositoryTrxn>();
        services.AddScoped<ITeamRepositoryQuery, TeamRepositoryQuery>();
        services.AddScoped<ICommentRepositoryTrxn, CommentRepositoryTrxn>();
        services.AddScoped<ICommentRepositoryQuery, CommentRepositoryQuery>();
        services.AddScoped<IAttachmentRepositoryTrxn, AttachmentRepositoryTrxn>();
        services.AddScoped<IAttachmentRepositoryQuery, AttachmentRepositoryQuery>();
        services.AddScoped<IReminderRepositoryTrxn, ReminderRepositoryTrxn>();
        services.AddScoped<IReminderRepositoryQuery, ReminderRepositoryQuery>();
        services.AddScoped<ITodoItemHistoryRepositoryTrxn, TodoItemHistoryRepositoryTrxn>();
        services.AddScoped<ITodoItemHistoryRepositoryQuery, TodoItemHistoryRepositoryQuery>();

        // Pattern: EF audit interceptor — sets CreatedBy/ModifiedBy from IRequestContext.
        services.AddTransient<AuditInterceptor<string, Guid?>>();

        // Pattern: Pooled DbContext factories with scoped wrappers.
        ConfigureDatabaseContexts(services, config);
    }

    private static void ConfigureDatabaseContexts(IServiceCollection services, IConfiguration config)
    {
        var connTrxn = config.GetConnectionString("TaskFlowDbContextTrxn")
            ?? throw new ArgumentException("TaskFlowDbContextTrxn connection string required");
        var connQuery = config.GetConnectionString("TaskFlowDbContextQuery")
            ?? throw new ArgumentException("TaskFlowDbContextQuery connection string required");

        // ── Transactional Context ──
        // Pattern: Pooled factory with audit interceptor + exception processor.
        services.AddPooledDbContextFactory<TaskFlowDbContextTrxn>((sp, options) =>
        {
            ConfigureSqlOptions(options, connTrxn, isAzure: connTrxn.Contains("database.windows.net"));
            options.UseExceptionProcessor();
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor<string, Guid?>>());
        });

        // Pattern: Scoped wrapper — resolves DbContext from factory per request.
        services.AddScoped<DbContextScopedFactory<TaskFlowDbContextTrxn, string, Guid?>>();
        services.AddScoped(sp =>
            sp.GetRequiredService<DbContextScopedFactory<TaskFlowDbContextTrxn, string, Guid?>>()
                .CreateDbContext());

        // ── Query Context ──
        // Pattern: Read-only context with NoTracking + ApplicationIntent=ReadOnly for read replicas.
        services.AddPooledDbContextFactory<TaskFlowDbContextQuery>((sp, options) =>
        {
            var readOnlyConn = connQuery.Contains("ApplicationIntent=")
                ? connQuery
                : connQuery + ";ApplicationIntent=ReadOnly";

            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            ConfigureSqlOptions(options, readOnlyConn, isAzure: connQuery.Contains("database.windows.net"));
            options.UseExceptionProcessor();
        });

        services.AddScoped<DbContextScopedFactory<TaskFlowDbContextQuery, string, Guid?>>();
        services.AddScoped(sp =>
            sp.GetRequiredService<DbContextScopedFactory<TaskFlowDbContextQuery, string, Guid?>>()
                .CreateDbContext());
    }

    /// <summary>
    /// Pattern: Azure SQL vs local SQL Server — different compatibility levels.
    /// Azure SQL: compat 170 (latest). Local SQL Server: compat 160 (SQL 2022).
    /// Both use EnableRetryOnFailure for transient fault handling.
    /// </summary>
    private static void ConfigureSqlOptions(
        DbContextOptionsBuilder options, string connectionString, bool isAzure)
    {
        if (isAzure)
        {
            options.UseAzureSql(connectionString, sql =>
            {
                sql.UseCompatibilityLevel(170);
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            });
        }
        else
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.UseCompatibilityLevel(160);
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Helpers — Caching (FusionCache)
    // Pattern: Named caches (Default, StaticData) with L1 memory + L2 Redis + backplane.
    // Configuration-driven: CacheSettings[] array in appsettings.
    // ═══════════════════════════════════════════════════════════════

    private static void AddCachingServices(IServiceCollection services, IConfiguration config)
    {
        var cacheSettingsList = config.GetSection("CacheSettings").Get<List<CacheSettings>>() ?? [];

        // Pattern: JSON serializer options with ReferenceHandler.Preserve to handle circular refs.
        var jsonOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        foreach (var settings in cacheSettingsList)
        {
            var fusionBuilder = services.AddFusionCache(settings.Name)
                .WithSystemTextJsonSerializer(jsonOptions)
                .WithCacheKeyPrefix($"{settings.Name}:")
                .WithDefaultEntryOptions(new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(settings.DurationMinutes),
                    DistributedCacheDuration = TimeSpan.FromMinutes(settings.DistributedCacheDurationMinutes),
                    IsFailSafeEnabled = true,
                    FailSafeMaxDuration = TimeSpan.FromMinutes(settings.FailSafeMaxDurationMinutes),
                    FailSafeThrottleDuration = TimeSpan.FromSeconds(settings.FailSafeThrottleDurationSeconds),
                    JitterMaxDuration = TimeSpan.FromSeconds(30),
                    FactorySoftTimeout = TimeSpan.FromSeconds(5),
                    FactoryHardTimeout = TimeSpan.FromSeconds(30)
                });

            // Pattern: Redis L2 + Backplane — only if connection string is configured.
            var redisConnString = config.GetConnectionString(settings.RedisConnectionStringName ?? "Redis1");
            if (!string.IsNullOrWhiteSpace(redisConnString))
            {
                fusionBuilder
                    .WithDistributedCache(
                        new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache(
                            Microsoft.Extensions.Options.Options.Create(
                                new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions
                                {
                                    Configuration = redisConnString
                                })))
                    .WithBackplane(
                        new ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis.RedisBackplane(
                            Microsoft.Extensions.Options.Options.Create(
                                new ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis.RedisBackplaneOptions
                                {
                                    Configuration = redisConnString
                                })));
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Helpers — Notifications
    // Pattern: Delegates to Infrastructure.Notification's extension method.
    // ═══════════════════════════════════════════════════════════════

    private static void AddNotificationServices(IServiceCollection services, IConfiguration config)
    {
        services.AddNotificationServices(config);
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Helpers — Startup Tasks
    // Pattern: IStartupTask implementations run sequentially after Build().
    // ═══════════════════════════════════════════════════════════════

    private static void AddStartupTasks(IServiceCollection services)
    {
        services.AddTransient<IStartupTask, ApplyEFMigrationsStartup>();
        services.AddTransient<IStartupTask, WarmupDependencies>();
        services.AddTransient<IStartupTask, LoadCacheStartup>();
    }
}
