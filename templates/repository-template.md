# Repository Template

| | |
|---|---|
| **File** | `Infrastructure.Repositories/{Entity}RepositoryTrxn.cs`, `{Entity}RepositoryQuery.cs` |
| **Depends on** | [entity-template](entity-template.md), [ef-configuration-template](ef-configuration-template.md) |
| **Referenced by** | [service-template](service-template.md), [bootstrapper.md](../skills/bootstrapper.md) |
| **Sampleapp** | `sample-app/src/Infrastructure/TaskFlow.Infrastructure.Repositories/TodoItemRepositoryTrxn.cs` |

## File: Infrastructure/Repositories/{Entity}RepositoryTrxn.cs

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using EF.Data;
using EF.Domain;

namespace Infrastructure.Repositories;

/// <summary>
/// RepositoryBase generic args: <TDbContext, TAuditId, TTenantId>
///   TAuditId = string (matches IRequestContext.AuditId type)
///   TTenantId = Guid? (matches ITenantEntity<Guid> — nullable for non-tenant scenarios)
/// </summary>
public class {Entity}RepositoryTrxn({Project}DbContextTrxn dbContext)
    : RepositoryBase<{Project}DbContextTrxn, string, Guid?>(dbContext), I{Entity}RepositoryTrxn
{
    // ===== Get with includes (entity-specific) =====
    public async Task<{Entity}?> Get{Entity}Async(Guid id, bool includeChildren = false, CancellationToken ct = default)
    {
        var includes = new List<Expression<Func<IQueryable<{Entity}>, IIncludableQueryable<{Entity}, object?>>>>();

        if (includeChildren)
        {
            includes.Add(q => q.Include(e => e.{ChildEntity}s));
        }

        return await GetEntityAsync(
            true,
            filter: e => e.Id == id,
            splitQueryThresholdOptions: SplitQueryThresholdOptions.Default,
            includes: [.. includes],
            cancellationToken: ct
        ).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    // ===== UpdateFromDto — delegates to Updater extension =====
    public DomainResult<{Entity}> UpdateFromDto({Entity} entity, {Entity}Dto dto,
        RelatedDeleteBehavior relatedDeleteBehavior = RelatedDeleteBehavior.None)
    {
        return DB.UpdateFromDto(entity, dto, relatedDeleteBehavior);
    }
}
```

## File: Infrastructure/Repositories/{Entity}RepositoryQuery.cs

```csharp
using EF.Data;
using EF.Common;

namespace Infrastructure.Repositories;

public class {Entity}RepositoryQuery({Project}DbContextQuery dbContext)
    : RepositoryBase<{Project}DbContextQuery, string, Guid?>(dbContext), I{Entity}RepositoryQuery
{
    // ===== Search with paging (uses inherited QueryPageProjectionAsync) =====
    public async Task<PagedResponse<{Entity}Dto>> Search{Entity}Async(
        SearchRequest<{Entity}SearchFilter> request, CancellationToken ct = default)
    {
        return await QueryPageProjectionAsync(
            {Entity}Mapper.ProjectorSearch,
            filter: BuildFilter(request.Filter),
            orderBy: BuildOrderBy(request),
            pageSize: request.PageSize,
            pageNumber: request.Page,
            cancellationToken: ct);
    }

    // ===== Lookup (autocomplete) =====
    public async Task<StaticList<StaticItem<Guid, Guid?>>> Lookup{Entity}Async(
        Guid? tenantId, string? search, CancellationToken ct = default)
    {
        Expression<Func<{Entity}, bool>>? filter = null;

        if (tenantId.HasValue && !string.IsNullOrWhiteSpace(search))
            filter = e => e.TenantId == tenantId && e.Name.Contains(search);
        else if (tenantId.HasValue)
            filter = e => e.TenantId == tenantId;
        else if (!string.IsNullOrWhiteSpace(search))
            filter = e => e.Name.Contains(search);

        return await QueryPageProjectionAsync(
            {Entity}Mapper.ProjectorStaticItems,
            filter: filter,
            orderBy: q => q.OrderBy(e => e.Name),
            pageSize: 50,
            pageNumber: 1,
            cancellationToken: ct)
            .ContinueWith(t => new StaticList<StaticItem<Guid, Guid?>> { Items = t.Result.Data }, ct);
    }

    // ===== Filter Builder =====
    private static Expression<Func<{Entity}, bool>>? BuildFilter({Entity}SearchFilter? filter)
    {
        if (filter == null) return null;

        return e =>
            (!filter.TenantId.HasValue || e.TenantId == filter.TenantId) &&
            (string.IsNullOrWhiteSpace(filter.Name) || e.Name.Contains(filter.Name)) &&
            (!filter.Flags.HasValue || e.Flags.HasFlag(filter.Flags.Value));
    }

    // ===== Order Builder =====
    private static Func<IQueryable<{Entity}>, IOrderedQueryable<{Entity}>> BuildOrderBy(
        SearchRequest<{Entity}SearchFilter> request)
    {
        var isDescending = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return request.SortBy?.ToLowerInvariant() switch
        {
            "name" => isDescending ? q => q.OrderByDescending(e => e.Name) : q => q.OrderBy(e => e.Name),
            _ => q => q.OrderBy(e => e.Name)  // Default sort
        };
    }
}
```

## File: Application/Contracts/Repositories/I{Entity}RepositoryTrxn.cs

```csharp
using EF.Data;
using EF.Domain;

namespace Application.Contracts.Repositories;

public interface I{Entity}RepositoryTrxn : IRepositoryBase
{
    Task<{Entity}?> Get{Entity}Async(Guid id, bool includeChildren = false, CancellationToken ct = default);
    DomainResult<{Entity}> UpdateFromDto({Entity} entity, {Entity}Dto dto,
        RelatedDeleteBehavior relatedDeleteBehavior = RelatedDeleteBehavior.None);

    // Inherited from RepositoryBase:
    // Task<T?> GetEntityAsync<T>(...)
    // void Create<T>(ref T entity)
    // void UpdateFull<T>(ref T entity)
    // Task DeleteAsync<T>(CancellationToken ct, params object[] keyValues)
    // void Delete<T>(T entity)
    // Task<int> SaveChangesAsync(OptimisticConcurrencyWinner winner, CancellationToken ct)
}
```

## File: Application/Contracts/Repositories/I{Entity}RepositoryQuery.cs

```csharp
using EF.Common;

namespace Application.Contracts.Repositories;

public interface I{Entity}RepositoryQuery
{
    Task<PagedResponse<{Entity}Dto>> Search{Entity}Async(SearchRequest<{Entity}SearchFilter> request, CancellationToken ct = default);
    Task<StaticList<StaticItem<Guid, Guid?>>> Lookup{Entity}Async(Guid? tenantId, string? search, CancellationToken ct = default);
}
```

## Critical: Delete Pattern (MUST call `Delete(entity)`)

The `Delete` method is inherited from `RepositoryBase`. It marks the entity for deletion in the change tracker. **You MUST call it before `SaveChangesAsync`** — simply loading an entity and saving will NOT delete it.

```csharp
// In service layer (not repository):
var entity = await repoTrxn.GetAsync(id, false, ct);
if (entity == null) return Result.Success(); // idempotent — not-found returns success
repoTrxn.Delete(entity);                     // marks for deletion
await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
return Result.Success();
```

> **BUG PATTERN:** Omitting `repoTrxn.Delete(entity)` causes delete operations to silently no-op. This was found and fixed in all 4 services during sample app TestContainer testing.

## Critical: SaveChangesAsync — NEVER Use 1-Param Overload

`DbContextBase.SaveChangesAsync(CancellationToken)` **ALWAYS throws `NotImplementedException`** by design. Always use the 2-param overload:

```csharp
// ✅ CORRECT — always use this
await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

// ❌ WRONG — throws NotImplementedException at runtime
await repoTrxn.SaveChangesAsync(ct);
```

The 2-param overload retries on `DbUpdateConcurrencyException` using the specified winner strategy.

## Notes

- **Repositories inherit `RepositoryBase<TContext, TAuditId, TTenantId>`** — provides `GetEntityAsync`, `Create(ref)`, `UpdateFull(ref)`, `Delete(entity)`, `DeleteAsync(predicate)`, `SaveChangesAsync(OptimisticConcurrencyWinner, CancellationToken)`, `QueryPageProjectionAsync`, `QueryPageAsync`
- **Generic args:** `TAuditId = string` (matches `IRequestContext.AuditId`), `TTenantId = Guid?` (matches `ITenantEntity<Guid>` — nullable for non-tenant scenarios)
- **Trxn repository**: Uses `{Project}DbContextTrxn` (tracking, audit interceptor, read-write)
- **Query repository**: Uses `{Project}DbContextQuery` (NoTracking, read-only replica)
- **UpdateFromDto** delegates to the static Updater extension method on the DbContext (see updater-template.md)
- Projectors (`{Entity}Mapper.ProjectorSearch`) used in query repo for efficient SQL translation
- No `SaveChangesAsync` override on query repo — read-only by design
- Entity-specific repositories for complex queries; `GenericRepositoryTrxn/Query` for simple CRUD
- Use `ConfigureAwait(ConfigureAwaitOptions.None)` in repository methods (library code)
