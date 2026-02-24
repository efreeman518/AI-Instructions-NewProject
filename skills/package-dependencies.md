# Package Dependencies

## Overview

The solution depends on shared infrastructure NuGet packages (`EF.*`) that provide base classes, utilities, and cross-cutting concerns. These are private/internal packages — not available on nuget.org. This document defines their key types and APIs so generated code is correct.

> **Source:** <https://github.com/efreeman518/EF.Packages>
>
> **Reference implementation:** See `sampleapp/src/Directory.Packages.props` for centralized package version management, and `sampleapp/src/nuget.config` for NuGet feed configuration including private feeds.

### Custom NuGet Feeds & Package Restore

The user **must** declare all custom NuGet feeds in `domain-inputs.schema.md` → `customNugetFeeds` at the start of the project. Without all feeds configured in `nuget.config`, the solution will fail to restore.

**Rules:**
1. `nuget.config` must include `nuget.org` **plus** every feed listed in `customNugetFeeds`.
2. After adding any new `<PackageReference>` to a project or `Directory.Packages.props`, **always update to the latest stable versions** from `nuget.org` and all configured custom feeds. Verify with `dotnet restore` after each change.
3. If a restore fails due to a missing package source, check `nuget.config` for the correct feed URL and authentication credentials.
4. Use `dotnet list package --outdated` to identify stale versions after initial scaffold, then update `Directory.Packages.props` to latest stable.
5. This applies to **every** phase of scaffolding — not just initial setup. Any time new packages are added (e.g., adding Aspire hosting packages, testing packages, Azure SDK packages), update all package versions to latest stable.

---

## EF.Domain

Base classes, interfaces, result patterns, and domain utilities. Namespace: `EF.Domain`.

### IEntityBase\<TId\>

```csharp
namespace EF.Domain;

public interface IEntityBase<TId>
{
    public TId Id { get; init; }
}
```

### EntityBase

```csharp
namespace EF.Domain;

public abstract class EntityBase : IEntityBase<Guid>
{
    private readonly Guid _id = Guid.CreateVersion7();

    public Guid Id
    {
        get { return _id; }
        init { if (value != Guid.Empty) _id = value; }
    }

    public byte[]? RowVersion { get; set; }
}
```

> **CRITICAL:** `EntityBase.Id` uses `Guid.CreateVersion7()` (time-sortable, .NET 10+), **NOT** `Guid.NewGuid()`. Always use `Guid.CreateVersion7()` when generating new Ids.

### AuditableBase\<TAuditIdType\>

```csharp
namespace EF.Domain;

public abstract class AuditableBase<TAuditIdType> : EntityBase, IAuditable<TAuditIdType>
{
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public TAuditIdType CreatedBy { get; set; } = default!;
    public DateTime? UpdatedDate { get; set; }
    public TAuditIdType? UpdatedBy { get; set; }
}
```

### IAuditable\<TAuditIdType\>

```csharp
namespace EF.Domain;

public interface IAuditable<TAuditIdType>
{
    DateTime CreatedDate { get; set; }
    TAuditIdType CreatedBy { get; set; }
    DateTime? UpdatedDate { get; set; }
    TAuditIdType? UpdatedBy { get; set; }
}
```

### ITenantEntity\<TTenantIdType\>

```csharp
namespace EF.Domain;

public interface ITenantEntity<TTenantIdType> where TTenantIdType : struct
{
    public TTenantIdType TenantId { get; init; }
}
```

### DomainError

```csharp
namespace EF.Domain.Contracts;

public record DomainError(string Error, string? Code = null)
{
    public static DomainError Create(string error, string? code = null) => new(error, code);
}
```

### DomainResult / DomainResult\<T\>

Railway-oriented result type for domain operations (namespace: `EF.Domain.Contracts`):

```csharp
namespace EF.Domain.Contracts;

public class DomainResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<DomainError> Errors { get; }
    public string ErrorMessage { get; }  // comma-joined error strings

    public static DomainResult Success();
    public static DomainResult Failure(string error);
    public static DomainResult Failure(IReadOnlyList<DomainError> errors);
    public static DomainResult Failure(Exception exception);
    public static DomainResult Combine(params DomainResult[] results);

    public DomainResult<TOut> Map<TOut>(TOut value);
    public DomainResult<TOut> Map<TOut>(Func<TOut> factory);

    public TOut Match<TOut>(Func<TOut> onSuccess, Func<IReadOnlyList<DomainError>, TOut> onFailure);
    public Task<TOut> MatchAsync<TOut>(Func<Task<TOut>> onSuccess, Func<IReadOnlyList<DomainError>, Task<TOut>> onFailure);

    public DomainResult OnSuccess(Action action);
    public DomainResult OnFailure(Action<IReadOnlyList<DomainError>> action);

    public static implicit operator bool(DomainResult result) => result.IsSuccess;
}

public class DomainResult<T> : DomainResult
{
    public T? Value { get; }
    public bool IsNone { get; }  // no value AND no errors

    public static DomainResult<T> Success(T value);
    public static new DomainResult<T> Failure(string error);
    public static new DomainResult<T> Failure(IReadOnlyList<DomainError> errors);
    public static new DomainResult<T> Failure(Exception exception);
    public static DomainResult<T> None();

    public DomainResult<TOut> Map<TOut>(Func<T, TOut> map);
    public DomainResult<TOut> Bind<TOut>(Func<T, DomainResult<TOut>> bind);
    public DomainResult<T> BindOrContinue<TIgnore>(Func<T, DomainResult<TIgnore>> bind);
    public DomainResult<T> Tap(Func<T, DomainResult> sideEffect);
    public DomainResult<T> Tap<TIgnore>(Func<T, DomainResult<TIgnore>> sideEffect);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IReadOnlyList<DomainError>, TOut> onFailure);
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IReadOnlyList<DomainError>, TOut> onFailure, Func<TOut> onNone);
    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<IReadOnlyList<DomainError>, Task<TOut>> onFailure);
    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<IReadOnlyList<DomainError>, Task<TOut>> onFailure, Func<Task<TOut>> onNone);

    public DomainResult<T> OnSuccess(Action<T> action);

    public static implicit operator DomainResult<T>(T value) => Success(value);
}
```

### DomainException

```csharp
namespace EF.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException();
    public DomainException(string message);
}
```

### CollectionUtility (Domain)

```csharp
namespace EF.Domain;

public static class CollectionUtility
{
    public static DomainResult SyncCollectionWithResult<TEntity, TDto, TId>(
        ICollection<TEntity> dbCollection,
        ICollection<TDto> dtoCollection,
        Func<TEntity, TId> getDbId,
        Func<TDto, TId?> getDtoId,
        Func<TDto, DomainResult> createFunc,
        Func<TEntity, TDto, DomainResult>? updateFunc = null,
        Func<TEntity, DomainResult>? removeFunc = null,
        bool failFast = false)
        where TId : struct, IEquatable<TId>;
}
```

### \[Mask\] Attribute

```csharp
namespace EF.Domain.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class MaskAttribute : Attribute { }
```

> Use `[Mask]` on properties containing PII. The `AuditInterceptor` reads this attribute and masks those property values in audit logs.

---

## EF.Domain.Contracts

`EF.Domain.Contracts` is a **separate NuGet package** containing contract types (interfaces, records, result types) from the domain layer. Projects that only need domain contracts (e.g., Application.Contracts) reference this package instead of the full `EF.Domain` package.

Key types: `DomainResult`, `DomainResult<T>`, `DomainError`, `IEntityBaseDto` — see EF.Domain section above for `DomainResult`/`DomainError` full API.

### IEntityBaseDto

```csharp
namespace EF.Domain.Contracts;

public interface IEntityBaseDto
{
    Guid? Id { get; set; }
}
```

---

## EF.Data + EF.Data.Contracts

EF Core base classes, repository pattern, interceptors, and query utilities.

### DbContextBase\<TAuditIdType, TTenantIdType\>

```csharp
namespace EF.Data;

public abstract class DbContextBase<TAuditIdType, TTenantIdType>(DbContextOptions options) : DbContext(options)
{
    public required TAuditIdType AuditId { get; set; }
    public TTenantIdType? TenantId { get; set; }

    // Builds a tenant query filter expression for use in OnModelCreating
    // GlobalAdmin (TenantId == null) sees all; tenant users see only their data
    protected LambdaExpression BuildTenantFilter(Type entityType);

    // SaveChangesAsync() without OptimisticConcurrencyWinner throws NotImplementedException
    // Auto-populates IAuditable CreatedDate/CreatedBy/UpdatedDate/UpdatedBy fields
    Task<int> SaveChangesAsync(OptimisticConcurrencyWinner winner,
        bool acceptAllChangesOnSuccess = true, int concurrencyExceptionRetries = 3,
        CancellationToken cancellationToken = default);
}
```

### IRepositoryBase

```csharp
namespace EF.Data.Contracts;

public interface IRepositoryBase
{
    Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> filter) where T : class;
    Task UpsertAsync<T>(T entity) where T : EntityBase;
    void Create<T>(ref T entity) where T : class;
    void PrepareForUpdate<T>(ref T entity) where T : EntityBase;
    void UpdateFull<T>(ref T entity) where T : EntityBase;
    void Delete<T>(T entity) where T : EntityBase;
    Task DeleteAsync<T>(CancellationToken cancellationToken = default, params object[] keys) where T : class;
    Task DeleteAsync<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) where T : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(OptimisticConcurrencyWinner winner, CancellationToken cancellationToken = default);

    void SetAutoDetectChanges(bool value);
    void DetectChanges();

    Task<T?> GetEntityByKeysAsync<T>(CancellationToken cancellationToken = default, params object[] keys) where T : class;

    Task<T?> GetEntityAsync<T>(bool tracking = false,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        SplitQueryThresholdOptions? splitQueryThresholdOptions = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<IQueryable<T>, IIncludableQueryable<T, object?>>>[] includes)
        where T : class;

    Task<PagedResponse<T>> QueryPageAsync<T>(bool readNoLock = false, bool tracking = false,
        int? pageSize = null, int? pageIndex = null,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        bool includeTotal = false, SplitQueryThresholdOptions? splitQueryThresholdOptions = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<IQueryable<T>, IIncludableQueryable<T, object?>>>[] includes)
        where T : class;

    Task<PagedResponse<TProject>> QueryPageProjectionAsync<T, TProject>(
        Expression<Func<T, TProject>> projector,
        bool readNoLock = false, int? pageSize = null, int? pageIndex = null,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        bool includeTotal = false, SplitQueryThresholdOptions? splitQueryThresholdOptions = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<IQueryable<T>, IIncludableQueryable<T, object?>>>[] includes)
        where T : class;

    IAsyncEnumerable<T> GetStream<T>(bool tracking = false,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        SplitQueryThresholdOptions? splitQueryThresholdOptions = null,
        params Expression<Func<IQueryable<T>, IIncludableQueryable<T, object?>>>[] includes)
        where T : class;

    IAsyncEnumerable<TProject> GetStreamProjection<T, TProject>(Func<T, TProject> projector,
        bool tracking = false, Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        SplitQueryThresholdOptions? splitQueryThresholdOptions = null,
        params Expression<Func<IQueryable<T>, IIncludableQueryable<T, object?>>>[] includes)
        where T : class;
}
```

### RepositoryBase\<TDbContext, TAuditIdType, TTenantIdType\>

```csharp
namespace EF.Data;

public abstract class RepositoryBase<TDbContext, TAuditIdType, TTenantIdType>(TDbContext dbContext)
    : IRepositoryBase where TDbContext : DbContextBase<TAuditIdType, TTenantIdType>
{
    protected TDbContext DB => dbContext;

    // Implements all IRepositoryBase methods.
    // QueryPage* methods return PagedResponse<T> with PageSize, PageIndex, Data, Total.
    // readNoLock: sets DB isolation level to READ UNCOMMITTED (for SQL Server only).
    // GetStream/GetStreamProjection: returns IAsyncEnumerable for streaming results.
}
```

### EntityBaseConfiguration\<T\>

```csharp
namespace EF.Data;

public abstract class EntityBaseConfiguration<T>(bool pkClusteredIndex = false)
    : IEntityTypeConfiguration<T> where T : EntityBase
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(e => e.Id).IsClustered(pkClusteredIndex);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}
```

### Supporting Types (EF.Data.Contracts)

```csharp
namespace EF.Data.Contracts;

public enum OptimisticConcurrencyWinner { ClientWins, DBWins, Throw }

public class SplitQueryThresholdOptions
{
    public bool ForceSplitQuery { get; set; }
    public int Threshold { get; set; }
    public static bool DetermineSplitQueryWithTotal(int? pageSize, int total,
        Expression<Func<IQueryable<object>, IIncludableQueryable<object, object?>>>[] includes,
        SplitQueryThresholdOptions? options);
}

// AuditChangeAttribute — marks entities/properties for audit table tracking
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class AuditChangeAttribute : Attribute { }
```

### AuditInterceptor

```csharp
namespace EF.Data.Interceptors;

// SaveChangesInterceptor that captures entity changes and publishes AuditEntry messages
// via IInternalMessageBus. Reads [Mask] attribute to mask PII in audit logs.
public class AuditInterceptor<TAuditIdType, TTenantIdType>(IInternalMessageBus msgBus)
    : SaveChangesInterceptor
{
    // SavingChangesAsync — captures changed entries, creates AuditEntry records
    // SavedChangesAsync — publishes audit entries via IInternalMessageBus
}
```

### DbContextScopedFactory

```csharp
namespace EF.Data;

// Creates scoped DbContext instances from a pooled factory, setting AuditId and TenantId
// from the current IRequestContext.
public class DbContextScopedFactory<TDbContext, TAuditIdType, TTenantIdType>(
    IDbContextFactory<TDbContext> pooledFactory,
    IRequestContext<TAuditIdType, TTenantIdType> requestContext)
    : IDbContextFactory<TDbContext>
    where TDbContext : DbContextBase<TAuditIdType, TTenantIdType>
{
    public TDbContext CreateDbContext();
}
```

### IQueryableExtensions

```csharp
namespace EF.Data.Contracts;

public static class IQueryableExtensions
{
    // Composes tracking, filter, orderBy, paging, includes, split query onto IQueryable
    public static IQueryable<T> ComposeIQueryable<T>(this IQueryable<T> query,
        bool tracking = false, int? pageSize = null, int? pageIndex = null,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool splitQuery = false,
        params Expression<Func<IQueryable<T>, IIncludableQueryable<T, object?>>>[] includes)
        where T : class;

    // IN clause for large collections (uses Contains for small sets, JOIN for large)
    public static IQueryable<T> WherePropertyIn<T, TKey>(this IQueryable<T> query,
        Expression<Func<T, TKey>> propertySelector, ICollection<TKey> values,
        int threshold = 30) where T : class;

    // Dynamic ordering by Sort collection
    public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, IEnumerable<Sort> sorts);

    // Dynamic ordering by SortSpec or string expressions
    public static IOrderedQueryable<T> OrderByProperty<T>(this IQueryable<T> source,
        string propertyPath, bool descending = false);
    public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, IEnumerable<SortSpec>? specs);
    public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, IEnumerable<string> sortExpressions);

    // Streaming
    public static IAsyncEnumerable<T> GetStream<T>(...) where T : class;
    public static IAsyncEnumerable<TProject> GetStreamProjection<T, TProject>(...) where T : class;

    // SortSpec record struct
    public readonly record struct SortSpec(string PropertyPath, bool Descending = false);
}
```

---

## EF.Common.Contracts

Shared contracts, interfaces, and value types used across the EF ecosystem. Namespace: `EF.Common.Contracts`.

### Result / Result\<T\>

```csharp
namespace EF.Common.Contracts;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<string> Errors { get; }
    public string ErrorMessage { get; }  // comma-joined errors

    public static Result Success();
    public static Result Failure(string error);
    public static Result Failure(IReadOnlyList<string> errors);
    public static Result Failure(Exception exception);

    public Result<TOut> Map<TOut>(TOut value);

    public static implicit operator bool(Result result) => result.IsSuccess;
}

public class Result<T> : Result
{
    public T? Value { get; }
    public bool IsNone { get; }  // no value AND no errors

    public static Result<T> Success(T value);
    public static new Result<T> Failure(string error);
    public static new Result<T> Failure(IReadOnlyList<string> errors);
    public static new Result<T> Failure(Exception exception);
    public static Result<T> None();

    public Result<TOut> Map<TOut>(Func<T, TOut> map);
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind);
    public Result<T> Tap(Func<T, Result> sideEffect);
    public Result<T> Tap<TIgnore>(Func<T, Result<TIgnore>> sideEffect);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IReadOnlyList<string>, TOut> onFailure);
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IReadOnlyList<string>, TOut> onFailure, Func<TOut> onNone);
    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<IReadOnlyList<string>, Task<TOut>> onFailure);
    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<IReadOnlyList<string>, Task<TOut>> onFailure, Func<Task<TOut>> onNone);

    public static implicit operator Result<T>(T value) => Success(value);
}
```

### IRequestContext

```csharp
namespace EF.Common.Contracts;

public interface IRequestContext<out TAuditIdType, out TTenantIdType>
{
    string CorrelationId { get; }
    TAuditIdType AuditId { get; }
    TTenantIdType? TenantId { get; }
    List<string> Roles { get; }
    bool RoleExists(string roleName);
}

// Concrete implementation — populated from JWT claims by middleware:
public class RequestContext<TAuditIdType, TTenantIdType>(
    string correlationId, TAuditIdType auditId, TTenantIdType? tenantId, List<string> roles)
    : IRequestContext<TAuditIdType, TTenantIdType>;
```

### PagedResponse\<T\>

```csharp
namespace EF.Common.Contracts;

public class PagedResponse<T>
{
    public int PageSize { get; set; }
    public int PageIndex { get; set; }
    public int Total { get; set; }
    public IReadOnlyList<T> Data { get; set; } = [];
}
```

### SearchRequest\<TFilter\>

```csharp
namespace EF.Common.Contracts;

public record SearchRequest<TFilter>
{
    public int PageSize { get; set; }
    public int PageIndex { get; set; }
    public IEnumerable<Sort>? Sorts { get; set; }
    public TFilter? Filter { get; set; }
}
```

### Sort / SortOrder

```csharp
namespace EF.Common.Contracts;

public class Sort(string propertyName, SortOrder sortOrder)
{
    public string PropertyName { get; set; } = propertyName;
    public SortOrder SortOrder { get; set; } = sortOrder;
}

public enum SortOrder
{
    Ascending = 0,
    Descending = 1
}
```

### IMessage

```csharp
namespace EF.Common.Contracts;

public interface IMessage { }
```

### AuditEntry\<TAuditIdType\>

```csharp
namespace EF.Common.Contracts;

public class AuditEntry<TAuditIdType>() : IMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required TAuditIdType AuditId { get; set; }
    public required string EntityType { get; set; }
    public required string EntityKey { get; set; }
    public AuditStatus Status { get; set; }
    public required string Action { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public string? Metadata { get; set; }
    public string? Error { get; set; }
}

public enum AuditStatus { Success = 0, Failure = 1 }
```

### Specification Pattern

```csharp
namespace EF.Common.Contracts;

public interface ISpecification { bool IsSatisfied(); }

public sealed class Specification(Func<bool> predicate) : ISpecification
{
    public IReadOnlyList<string> Messages { get; }
    public void AddMessage(string message);
    public bool IsSatisfied() => predicate();

    // Operators: &, |, !
}

public interface ISpecification<in T> { bool IsSatisfied(T candidate); }

public class Specification<T> : ISpecification<T>
{
    // Constructor takes Expression<Func<T, bool>>
    // Operators: &, |, !
    // Mixed chaining with non-generic Specification via MixedSpecificationExtensions
}
```

---

## EF.Common

Shared utilities, extensions, and cross-cutting concerns. Namespace: `EF.Common`.

### ResultExtensions

```csharp
namespace EF.Common.Extensions;

public static class ResultExtensions
{
    // Converts DomainResult → Result (domain layer to application layer)
    public static Result ToResult(this DomainResult domainResult);
    public static Result<T> ToResult<T>(this DomainResult<T> domainResult);
}
```

### PredicateBuilder

```csharp
namespace EF.Common;

public static class PredicateBuilder
{
    public static Expression<Func<T, bool>> True<T>();
    public static Expression<Func<T, bool>> False<T>();
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2);
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2);
}
```

### ExpressionExtensions

```csharp
namespace EF.Common.Extensions;

public static class ExpressionExtensions
{
    // Fluent expression tree builders (EF Core friendly, Invoke-free)
    public static Expression<Func<T, bool>> In<T, TProp>(...);
    public static Expression<Func<T, bool>> NotIn<T, TProp>(...);
    public static Expression<Func<T, bool>> Between<T, TProp>(...);
    public static Expression<Func<T, bool>> ContainsCI<T>(...);  // case-insensitive Contains
    public static Expression<Func<T, bool>> StartsWithCI<T>(...);
    public static Expression<Func<T, bool>> EndsWithCI<T>(...);
    public static Expression<Func<T, object?>> BuildObjectSelector<T>(string propertyPath);
}
```

### CollectionUtility (Common)

```csharp
namespace EF.Common;

public static class CollectionUtility
{
    // Non-DomainResult overloads for simple collection sync operations
    // (The DomainResult-returning overload is in EF.Domain.CollectionUtility)
}
```

### NotFoundException

```csharp
namespace EF.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException();
    public NotFoundException(string message);
    public NotFoundException(string message, Exception innerException);
}
```

---

## EF.Host

Hosting utilities.

### StaticLogging

```csharp
public static class StaticLogging
{
    public static ILogger Logger { get; set; }  // Set at startup for pre-DI logging
}
```

---

## EF.BackgroundService

Background task processing, CRON jobs, and internal message bus. NuGet package: `EF.BackgroundServices`. Namespace: `EF.BackgroundServices`.

### IInternalMessageBus

```csharp
namespace EF.BackgroundServices.InternalMessageBus;

public interface IInternalMessageBus
{
    void AutoRegisterHandlers(IServiceProvider serviceProvider, params Assembly[] assemblies);
    void RegisterMessageHandler<T>(IMessageHandler<T> handler) where T : IMessage;
    void UnregisterMessageHandler<T>(IMessageHandler<T> handler) where T : IMessage;
    Task Publish<T>(InternalMessageBusProcessMode mode, ICollection<T> messages,
        CancellationToken cancellationToken = default) where T : IMessage;
}

public enum InternalMessageBusProcessMode
{
    Queue = 1,  // One handler per message (competing consumers)
    Topic        // All handlers receive each message (fan-out)
}
```

### IMessageHandler\<T\>

```csharp
namespace EF.BackgroundServices.InternalMessageBus;

public interface IMessageHandler<in T> where T : IMessage
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
```

> **CRITICAL:** `IInternalMessageBus` and `IMessageHandler<T>` are in the `EF.BackgroundServices` NuGet package, namespace `EF.BackgroundServices.InternalMessageBus`. They were previously in `EF.Common`.

### IBackgroundTaskQueue

```csharp
namespace EF.BackgroundServices;

public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, ValueTask> workItem);
    ValueTask QueueScopedBackgroundWorkItem<TScoped>(
        Func<TScoped, CancellationToken, ValueTask> workItem) where TScoped : notnull;
    ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
}
```

### ChannelBackgroundTaskQueue / ChannelBackgroundTaskService

```csharp
namespace EF.BackgroundServices;

// Channel-based implementation of IBackgroundTaskQueue
public class ChannelBackgroundTaskQueue : IBackgroundTaskQueue { }

// BackgroundService that processes items from the queue
public class ChannelBackgroundTaskService : BackgroundService { }
```

### CronBackgroundService\<T\> / ICronJobHandler\<T\>

```csharp
namespace EF.BackgroundServices;

public abstract class CronBackgroundService<T> : BackgroundService
    where T : ICronJobHandler<T>;

public interface ICronJobHandler<T>
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}

public class CronJobSettings
{
    public string CronExpression { get; set; } = null!;
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
}
```

### ScopedBackgroundService

```csharp
namespace EF.BackgroundServices;

// Abstract BackgroundService that creates a new DI scope for each execution
public abstract class ScopedBackgroundService : BackgroundService { }
```

### ServiceCollectionExtensions

```csharp
namespace EF.BackgroundServices;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundTaskQueue(this IServiceCollection services);
    public static IServiceCollection AddChannelBackgroundTaskQueue(this IServiceCollection services);
    public static IServiceCollection AddCronJob<TJobSettings, THandler>(this IServiceCollection services,
        IConfiguration configuration) where TJobSettings : CronJobSettings, new()
        where THandler : class, ICronJobHandler<THandler>;
}
```

---

## EF.CosmosDb

Cosmos DB repository pattern with partition key support. See [cosmosdb-data.md](cosmosdb-data.md) for full scaffolding guide.

### CosmosDbEntity

```csharp
namespace EF.CosmosDb;

public abstract class CosmosDbEntity : EntityBase
{
    public abstract string PartitionKey { get; }
    public string id => Id.ToString();  // Cosmos DB requires lowercase 'id'
}
```

### ICosmosDbRepository

```csharp
namespace EF.CosmosDb;

public interface ICosmosDbRepository
{
    Task<T> SaveItemAsync<T>(T item) where T : CosmosDbEntity;
    Task<T?> GetItemAsync<T>(string id, string partitionKey) where T : CosmosDbEntity;
    Task DeleteItemAsync<T>(string id, string partitionKey);
    Task DeleteItemAsync<T>(T item) where T : CosmosDbEntity;

    // LINQ-based paged query with filter and sorting
    Task<(List<TProject>, int, string?)> QueryPageProjectionAsync<TSource, TProject>(
        string? continuationToken = null, int pageSize = 10,
        Expression<Func<TProject, bool>>? filter = null,
        List<Sort>? sorts = null, bool includeTotal = false,
        int maxConcurrency = -1, CancellationToken cancellationToken = default);

    // SQL-based paged query
    Task<(List<TProject>, int, string?)> QueryPageProjectionAsync<TSource, TProject>(
        string? continuationToken = null, int pageSize = 10,
        string? sql = null, string? sqlCount = null,
        Dictionary<string, object>? parameters = null,
        int maxConcurrency = -1, CancellationToken cancellationToken = default);

    // Streaming
    IAsyncEnumerable<T> GetStream<T>(Expression<Func<T, bool>>? filter = null,
        List<Sort>? sorts = null, int maxConcurrency = -1);
    IAsyncEnumerable<T> GetStream<T>(string? sql = null,
        Dictionary<string, object>? parameters = null, int maxConcurrency = -1);
}
```

### CosmosDbRepositoryBase

```csharp
namespace EF.CosmosDb;

public abstract class CosmosDbRepositoryBase : ICosmosDbRepository
{
    // Constructor: ILogger<CosmosDbRepositoryBase>, IOptions<CosmosDbRepositorySettingsBase>
    // Resolves CosmosClient and database from settings
}
```

### CosmosDbRepositorySettingsBase

```csharp
namespace EF.CosmosDb;

public class CosmosDbRepositorySettingsBase
{
    public CosmosClient CosmosClient { get; set; } = null!;
    public string CosmosDbId { get; set; } = null!;
}
```

---

## EF.Storage

Azure Blob Storage repository. See [blob-storage.md](blob-storage.md) for full scaffolding guide.

### IBlobRepository

```csharp
public interface IBlobRepository
{
    Task<BlobProperties?> UploadAsync(string containerName, string blobName, Stream content,
        string? contentType = null, IDictionary<string, string>? metadata = null,
        CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default);
    Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken ct = default);
    Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken ct = default);

    Task<Uri> GetSasUriAsync(string containerName, string blobName,
        BlobSasPermissions permissions, TimeSpan expiry, CancellationToken ct = default);

    Task<BlobContainerClient> GetContainerClientAsync(string containerName, CancellationToken ct = default);

    // Distributed lock
    Task<string?> AcquireLeaseAsync(string containerName, string blobName,
        TimeSpan duration, CancellationToken ct = default);
    Task ReleaseLeaseAsync(string containerName, string blobName,
        string leaseId, CancellationToken ct = default);
}
```

### BlobRepositoryBase

```csharp
public abstract class BlobRepositoryBase : IBlobRepository
{
    // Constructor: IAzureClientFactory<BlobServiceClient>, IOptions<TSettings>
}
```

### BlobRepositorySettingsBase

```csharp
public class BlobRepositorySettingsBase
{
    public string ClientName { get; set; } = null!;
    public List<ContainerInfo> Containers { get; set; } = [];
}

public class ContainerInfo
{
    public string ContainerName { get; set; } = null!;
    public ContainerPublicAccessType PublicAccessType { get; set; } = ContainerPublicAccessType.None;
}
```

---

## EF.Table

Azure Table Storage repository. See [table-storage.md](table-storage.md) for full scaffolding guide.

### ITableRepository

```csharp
public interface ITableRepository
{
    Task<T?> GetEntityAsync<T>(string partitionKey, string rowKey,
        CancellationToken ct = default) where T : class, ITableEntity, new();
    Task UpsertEntityAsync<T>(T entity,
        TableUpdateMode mode = TableUpdateMode.Merge,
        CancellationToken ct = default) where T : class, ITableEntity, new();
    Task DeleteEntityAsync(string partitionKey, string rowKey,
        CancellationToken ct = default);

    Task<(IReadOnlyList<T>?, string?)> QueryPageAsync<T>(
        string? continuationToken = null, int pageSize = 10,
        Expression<Func<T, bool>>? filterLinq = null, string? filterOData = null,
        IEnumerable<string>? selectProps = null,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity;

    IAsyncEnumerable<T> GetStream<T>(
        Expression<Func<T, bool>>? filterLinq = null, string? filterOData = null,
        IEnumerable<string>? selectProps = null,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity;
}
```

### TableRepositoryBase

```csharp
public abstract class TableRepositoryBase : ITableRepository
{
    // Constructor: TableServiceClient
}
```

### TableRepositorySettingsBase

```csharp
public class TableRepositorySettingsBase
{
    public string ClientName { get; set; } = null!;
    public string TableName { get; set; } = null!;
}
```

---

## EF.Messaging

Service Bus, Event Grid, and Event Hub messaging abstractions. See [messaging.md](messaging.md) for full scaffolding guide.

### Service Bus — Sender

```csharp
public interface IServiceBusSender
{
    Task SendMessageAsync<T>(T message, string? sessionId = null,
        string? correlationId = null, IDictionary<string, object>? properties = null,
        CancellationToken ct = default);
    Task SendMessagesAsync<T>(IEnumerable<T> messages, CancellationToken ct = default);
    Task ScheduleMessageAsync<T>(T message, DateTimeOffset scheduledEnqueueTime,
        CancellationToken ct = default);
}

public abstract class ServiceBusSenderBase : IServiceBusSender
{
    // Constructor: IAzureClientFactory<ServiceBusClient>, IOptions<TSettings>
}
```

### Service Bus — Receiver / Processor

```csharp
public interface IServiceBusReceiver
{
    Task<IReadOnlyList<T>> ReceiveMessagesAsync<T>(int maxMessages = 10,
        TimeSpan? maxWaitTime = null, CancellationToken ct = default);
    Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken ct = default);
    Task AbandonMessageAsync(ServiceBusReceivedMessage message, CancellationToken ct = default);
    Task DeadLetterMessageAsync(ServiceBusReceivedMessage message,
        string reason, CancellationToken ct = default);
}

public abstract class ServiceBusProcessorBase : BackgroundService
{
    // Constructor: IAzureClientFactory<ServiceBusClient>, IOptions<TSettings>, IServiceProvider
    // Override: Task ProcessMessageAsync(ProcessMessageEventArgs args, CancellationToken ct)
    // Override: Task ProcessErrorAsync(ProcessErrorEventArgs args, CancellationToken ct)
}
```

### Event Grid — Publisher

```csharp
public interface IEventGridPublisher
{
    Task PublishEventAsync<T>(string eventType, string subject, T data,
        CancellationToken ct = default);
    Task PublishEventsAsync<T>(IEnumerable<EventGridEvent> events,
        CancellationToken ct = default);
}

public abstract class EventGridPublisherBase : IEventGridPublisher
{
    // Constructor: IAzureClientFactory<EventGridPublisherClient>, IOptions<TSettings>
}
```

### Event Hub — Producer / Processor

```csharp
public interface IEventHubProducer
{
    Task SendAsync<T>(T data, string? partitionKey = null, CancellationToken ct = default);
    Task SendBatchAsync<T>(IEnumerable<T> data, string? partitionKey = null,
        CancellationToken ct = default);
}

public abstract class EventHubProducerBase : IEventHubProducer
{
    // Constructor: IAzureClientFactory<EventHubProducerClient>, IOptions<TSettings>
}

public interface IEventHubProcessor
{
    Task StartProcessingAsync(CancellationToken ct = default);
    Task StopProcessingAsync(CancellationToken ct = default);
}

public abstract class EventHubProcessorBase : BackgroundService, IEventHubProcessor
{
    // Constructor: EventProcessorClient, IServiceProvider
    // Override: Task ProcessEventAsync(ProcessEventArgs args, CancellationToken ct)
    // Override: Task ProcessErrorAsync(ProcessErrorEventArgs args, CancellationToken ct)
}
```

---

## EF.KeyVault

Azure Key Vault management and field-level encryption. See [keyvault.md](keyvault.md) for full scaffolding guide.

### IKeyVaultManager

```csharp
public interface IKeyVaultManager
{
    // Secrets
    Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default);
    Task SetSecretAsync(string secretName, string value, CancellationToken ct = default);
    Task DeleteSecretAsync(string secretName, CancellationToken ct = default);

    // Keys
    Task<KeyVaultKey> GetKeyAsync(string keyName, CancellationToken ct = default);
    Task<KeyVaultKey> CreateKeyAsync(string keyName, KeyType keyType, CancellationToken ct = default);

    // Certificates
    Task<KeyVaultCertificateWithPolicy> GetCertificateAsync(string certName, CancellationToken ct = default);
}
```

### IKeyVaultCryptoUtility

```csharp
public interface IKeyVaultCryptoUtility
{
    Task<string> EncryptAsync(string keyName, string plaintext, CancellationToken ct = default);
    Task<string> DecryptAsync(string keyName, string ciphertext, CancellationToken ct = default);
}
```

### KeyVaultManagerSettingsBase

```csharp
public class KeyVaultManagerSettingsBase
{
    public string VaultUri { get; set; } = null!;
    public string SecretClientName { get; set; } = null!;
    public string KeyClientName { get; set; } = null!;
    public string CertificateClientName { get; set; } = null!;
}
```

---

## EF.Grpc

gRPC error interceptor pattern. See [grpc.md](grpc.md) for full scaffolding guide.

### Error Interceptors

```csharp
// Client-side — converts RpcException to standard error responses
public class ClientErrorInterceptor(ILogger<ClientErrorInterceptor> logger,
    IOptions<ErrorInterceptorSettings> settings) : Interceptor
{
    // Wraps all unary/streaming calls with try/catch → ProblemDetails
}

// Server-side — converts unhandled exceptions to RpcException with proper status codes
public class ServiceErrorInterceptor(ILogger<ServiceErrorInterceptor> logger,
    IOptions<ErrorInterceptorSettings> settings) : Interceptor
{
    // Maps exception types to gRPC status codes
}

public class ErrorInterceptorSettings
{
    public bool IncludeExceptionDetails { get; set; }  // true in Development
}
```

### DI Registration Extension

```csharp
public static class IServiceCollectionExtensions
{
    // Registers ClientErrorInterceptor or ServiceErrorInterceptor with options
    public static IServiceCollection AddGrpcErrorInterceptors(
        this IServiceCollection services, IConfiguration configuration);
}
```

---

## EF.FilterBuilder

Dynamic filter expression builder for constructing LINQ `Expression<Func<T, bool>>` from structured filter definitions. Useful for building dynamic search/filter endpoints.

### FilterSet / Filter

```csharp
namespace EF.FilterBuilder.Contracts;

public class FilterSet
{
    public Operator Operator { get; set; }
    public List<Filter>? Filters { get; set; }
    public List<FilterSet>? FilterSets { get; set; }  // nested groups
}

public class Filter
{
    public string PropertyPath { get; set; } = "";
    public string? Operation { get; set; }     // e.g. "==", "!=", "Contains", "StartsWith"
    public FilterType FilterType { get; set; }
    public string? ParamKey { get; set; }
    public string? TestValue { get; set; }      // right-side value
}

public enum Operator { AND = 1, OR = 2 }
public enum FilterType { Default = 0, TestValueParam = 1, TestValueFilterExpression = 2, TestValueJson = 3 }
```

### SearchRequest / SearchResponse (FilterBuilder)

```csharp
namespace EF.FilterBuilder.Contracts;

public class SearchRequest<T> where T : class
{
    public int PageSize { get; set; }
    public int PageIndex { get; set; }
    public FilterSet? FilterSet { get; set; }
    public List<Sort>? Sorts { get; set; }
    public T? FilterItem { get; set; }
}

public class SearchResponse<T>
{
    public int PageSize { get; set; }
    public int PageIndex { get; set; }
    public List<T>? Data { get; set; }
    public long Total { get; set; }
}
```

> Note: `EF.FilterBuilder.Contracts` defines its own `Sort` and `SortOrder` types (identical to `EF.Common.Contracts` versions).

---

## External API Integration Packages (Public NuGet)

These are **public NuGet packages** (not private infrastructure packages) used by `Infrastructure.{ServiceName}` projects for external API integration. See [external-api.md](external-api.md) for full scaffolding guide.

### Refit.HttpClientFactory

Generates `HttpClient` implementations from C# interfaces at compile time.

```csharp
// Registration — creates a named HttpClient backed by the Refit interface
services.AddRefitClient<I{ServiceName}Api>(new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(jsonOptions)
})
.ConfigureHttpClient(c => c.BaseAddress = new Uri(settings.BaseUrl))
.AddHttpMessageHandler<{ServiceName}AuthHandler>();
```

### Microsoft.Extensions.Http.Resilience

Provides `.AddResilienceHandler()` and `.AddStandardResilienceHandler()` for `IHttpClientBuilder`. Built on top of Polly v8.

```csharp
// Per-client resilience pipeline (replaces global StandardResilienceHandler for this client)
.AddResilienceHandler("{ServiceName}", (builder, context) =>
{
    builder.AddRetry(new HttpRetryStrategyOptions { /* ... */ });
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions { /* ... */ });
    builder.AddTimeout(TimeSpan.FromSeconds(10));
});
```

| Strategy | Class | Purpose |
|----------|-------|---------|
| Retry | `HttpRetryStrategyOptions` | Exponential backoff + jitter for transient failures |
| Circuit Breaker | `HttpCircuitBreakerStrategyOptions` | Short-circuit calls when failure ratio exceeds threshold |
| Timeout | `AddTimeout(TimeSpan)` | Per-attempt timeout (inside retries) |
| Hedging | `HttpHedgingStrategyOptions` | Parallel request to fallback endpoint after delay |

> Both packages must be versioned in `Directory.Packages.props`. They are public NuGet packages, not private.

---

## Verification

After setting up package dependencies, confirm:

- [ ] `nuget.config` includes both the private NuGet feed and `nuget.org`
- [ ] `Directory.Packages.props` manages all package versions centrally (projects use `<PackageReference>` without `Version`)
- [ ] `global.json` pins the .NET SDK version
- [ ] `EF.*` packages are installed in the correct projects (Domain, Data, Common, Host, BackgroundService, and any CosmosDb/Storage/Table/Messaging/KeyVault/Grpc/FilterBuilder packages where needed)
- [ ] No version conflicts between private packages and public NuGet packages
- [ ] All packages are updated to the **latest stable versions** from nuget.org and custom feeds after adding references
- [ ] Interface contracts from private packages (`IRepositoryBase`, `IInternalMessageBus`, `IMessageHandler<T>`, `IBackgroundTaskQueue`, `ICosmosDbRepository`, `IBlobRepository`, `ITableRepository`, `IServiceBusSender`, `IKeyVaultManager`, etc.) are implemented correctly
- [ ] Azure SDK client factories (`IAzureClientFactory<T>`) are registered with named clients for all infrastructure packages that require them
- [ ] Cross-references: Package versions match [solution-structure.md](solution-structure.md) dependency flow, Bootstrapper registers all package-provided interfaces
- [ ] `IInternalMessageBus` / `IMessageHandler<T>` are imported from `EF.BackgroundServices.InternalMessageBus` namespace (from `EF.BackgroundServices` NuGet package)
- [ ] `EntityBase.Id` uses `Guid.CreateVersion7()` — never `Guid.NewGuid()` for entity IDs
- [ ] `DomainError` is used as `public record DomainError(string Error, string? Code = null)` — not a class with `Message` property
- [ ] `DomainResult.Errors` is `IReadOnlyList<DomainError>` — not `IReadOnlyList<string>`
- [ ] `Result.Errors` is `IReadOnlyList<string>` — convert via `ResultExtensions.ToResult()` when crossing domain-to-application boundary
