# Updater Template

| | |
|---|---|
| **File** | `Infrastructure.Repositories/Updaters/{Entity}Updater.cs` |
| **Depends on** | [entity-template](entity-template.md), [data-mapping-template](data-mapping-template.md) |
| **Referenced by** | [repository-template](repository-template.md) |

## File: Infrastructure/Repositories/Updaters/{Entity}Updater.cs

The updater is a **DbContext extension method** - this gives it access to `db.Delete()` for explicit EF change-tracker removal of orphaned children.

```csharp
using EF.Data;
using EF.Data.Contracts;
using EF.Domain;
using EF.Domain.Contracts;

namespace Infrastructure.Repositories.Updaters;

internal static class {Entity}Updater
{
    /// <summary>
    /// Updates scalar properties then syncs child collections via railway pattern.
    /// Extension on DbContextTrxn for access to db.Delete().
    /// </summary>
    public static DomainResult<{Entity}> UpdateFromDto(
        this {Project}DbContextTrxn db,
        {Entity} entity,
        {Entity}Dto dto,
        RelatedDeleteBehavior relatedDeleteBehavior = RelatedDeleteBehavior.None)
    {
        return entity.Update(
            name: dto.Name,
            description: dto.Description)
        .Bind(updatedEntity => DomainResult.Combine(
            // Sync {ChildEntity}s collection (owned, 1:N)
            CollectionUtility.SyncCollectionWithResult<{ChildEntity}, {ChildEntity}Dto, Guid>(
                updatedEntity.{ChildEntity}s,
                dto.{ChildEntity}s ?? [],
                e => e.Id,
                i => i.Id,
                incomingDto =>
                {
                    var result = {ChildEntity}.Create(updatedEntity.TenantId, updatedEntity.Id, incomingDto.Name);
                    if (result.IsSuccess) updatedEntity.{ChildEntity}s.Add(result.Value!);
                    return result;
                },
                (existing, incomingDto) => existing.Update(incomingDto.Name),
                toRemove =>
                {
                    if (relatedDeleteBehavior == RelatedDeleteBehavior.None) return DomainResult.Success();
                    db.Delete(toRemove);
                    updatedEntity.{ChildEntity}s.Remove(toRemove);
                    return DomainResult.Success();
                }
            ),
            // Sync Tags collection (M:N via junction entity)
            CollectionUtility.SyncCollectionWithResult<{Entity}Tag, TagDto, Guid>(
                updatedEntity.{Entity}Tags,
                dto.Tags ?? [],
                e => e.TagId,
                i => i.Id,
                incomingDto =>
                {
                    var result = {Entity}Tag.Create(updatedEntity.TenantId, updatedEntity.Id, incomingDto.Id!.Value);
                    if (result.IsSuccess) updatedEntity.{Entity}Tags.Add(result.Value!);
                    return result;
                },
                // updateFunc omitted - junction has no updatable properties
                removeFunc: toRemove =>
                {
                    if (relatedDeleteBehavior == RelatedDeleteBehavior.None) return DomainResult.Success();
                    db.Delete(toRemove);
                    updatedEntity.{Entity}Tags.Remove(toRemove);
                    return DomainResult.Success();
                }
            ))
            .Map(updatedEntity)
        );
    }
}
```

## CollectionUtility.SyncCollectionWithResult (from EF.Domain)

Generic utility for synchronizing two collections with DomainResult-based error aggregation.

```csharp
namespace EF.Domain;

public static class CollectionUtility
{
    /// <summary>
    /// Synchronize a database collection with an incoming DTO collection.
    /// Matches by key; creates new items, updates matching items, removes missing items.
    /// Error aggregation is handled internally - returns a combined DomainResult.
    /// </summary>
    /// <typeparam name="TEntity">Entity type in the database collection.</typeparam>
    /// <typeparam name="TDto">DTO type in the incoming collection.</typeparam>
    /// <typeparam name="TId">Key type (must be struct + IEquatable).</typeparam>
    /// <param name="dbCollection">The entity's navigation collection (ICollection).</param>
    /// <param name="dtoCollection">The incoming DTOs with desired state.</param>
    /// <param name="getDbId">Key selector for entity.</param>
    /// <param name="getDtoId">Key selector for DTO (returns TId? - null/default = new item).</param>
    /// <param name="createFunc">Creates entity from DTO. Must add to collection if successful. Returns DomainResult.</param>
    /// <param name="updateFunc">Optional. Updates existing entity from DTO. Returns DomainResult.</param>
    /// <param name="removeFunc">Optional. Removes entity from collection. Returns DomainResult. If null, no deletes occur (partial update).</param>
    /// <param name="failFast">If true, stops on first failure. Default: false.</param>
    public static DomainResult SyncCollectionWithResult<TEntity, TDto, TId>(
        ICollection<TEntity> dbCollection,
        ICollection<TDto> dtoCollection,
        Func<TEntity, TId> getDbId,
        Func<TDto, TId?> getDtoId,
        Func<TDto, DomainResult> createFunc,
        Func<TEntity, TDto, DomainResult>? updateFunc = null,
        Func<TEntity, DomainResult>? removeFunc = null,
        bool failFast = false)
        where TId : struct, IEquatable<TId>
    { ... }
}
```

### Key Behaviors

- **Create:** When `getDtoId` returns null, default, or a key not found in `dbCollection`, calls `createFunc`. The create lambda must add the new entity to the parent's collection itself.
- **Update:** When a matching key exists in both collections, calls `updateFunc` (if provided). The matched entity is removed from the internal lookup so it won't be deleted.
- **Remove:** After processing all DTOs, any entities remaining in the lookup (not matched) are passed to `removeFunc` (if provided). If `removeFunc` is null, unmatched entities are left alone (partial update mode).
- **Error aggregation:** All results are collected and combined via `DomainResult.Combine()`. With `failFast: true`, stops on first failure.

### Parameter Order Mnemonic

`db, dto, dbKey, dtoKey, create, update?, remove?, failFast?`

## Patterns

### Owned 1:N Child (Comments, ChecklistItems)

All three callbacks needed - create adds to collection, update delegates to entity method, remove calls `db.Delete()` to mark for EF deletion:

```csharp
CollectionUtility.SyncCollectionWithResult<Comment, CommentDto, Guid>(
    updatedEntity.Comments,
    dto.Comments ?? [],
    e => e.Id,
    i => i.Id,
    createFunc: incomingDto =>
    {
        var result = Comment.Create(updatedEntity.TenantId, updatedEntity.Id, incomingDto.Body);
        if (result.IsSuccess) updatedEntity.Comments.Add(result.Value!);
        return result;
    },
    updateFunc: (existing, incomingDto) => existing.Update(incomingDto.Body),
    removeFunc: toRemove =>
    {
        if (relatedDeleteBehavior == RelatedDeleteBehavior.None) return DomainResult.Success();
        db.Delete(toRemove);
        updatedEntity.Comments.Remove(toRemove);
        return DomainResult.Success();
    });
```

#### createFunc must apply ALL DTO fields

Domain factory methods often take a minimal field set (e.g., `ChecklistItem.Create(tenantId, taskItemId, title, sortOrder)` - no `IsCompleted`). If the DTO carries additional state (a pre-checked checkbox buffered in a create form, a status flag, a completion date), the `createFunc` must follow the `Create` with an `Update` call to apply those fields. Otherwise the UI's single-payload aggregate save silently drops them on newly-inserted children. Pattern:

```csharp
createFunc: incomingDto =>
{
    var result = ChecklistItem.Create(updatedEntity.TenantId, updatedEntity.Id, incomingDto.Title, incomingDto.SortOrder);
    if (result.IsSuccess)
    {
        // Create() has no IsCompleted arg - apply it via Update() so buffered
        // "checked" state isn't lost when the parent + children are POSTed together.
        if (incomingDto.IsCompleted) result.Value!.Update(isCompleted: true);
        updatedEntity.Comments.Add(result.Value!);
    }
    return result;
}
```

Rule: for every field the DTO can carry that the domain factory doesn't accept, the `createFunc` must call the corresponding `Update` / setter path immediately after `Create`.

### M:N Junction (Tags via TaskItemTag)

Only create + remove needed - junction entities have no updatable properties. Match on the foreign key (TagId), not the junction entity's own Id:

```csharp
CollectionUtility.SyncCollectionWithResult<TaskItemTag, TagDto, Guid>(
    updatedEntity.TaskItemTags,
    dto.Tags ?? [],
    e => e.TagId,           // match on FK, not junction Id
    i => i.Id,
    createFunc: incomingDto =>
    {
        var result = TaskItemTag.Create(updatedEntity.TenantId, updatedEntity.Id, incomingDto.Id!.Value);
        if (result.IsSuccess) updatedEntity.TaskItemTags.Add(result.Value!);
        return result;
    },
    removeFunc: toRemove =>
    {
        if (relatedDeleteBehavior == RelatedDeleteBehavior.None) return DomainResult.Success();
        db.Delete(toRemove);
        updatedEntity.TaskItemTags.Remove(toRemove);
        return DomainResult.Success();
    });
    // updateFunc omitted - no properties to update on junction
```

### Partial Update (no removes)

Omit `removeFunc` to allow adding/updating without deleting unmatched entities:

```csharp
CollectionUtility.SyncCollectionWithResult<Address, AddressDto, Guid>(
    updatedEntity.Addresses,
    dto.Addresses ?? [],
    e => e.Id,
    i => i.Id,
    createFunc: incomingDto => { ... },
    updateFunc: (existing, incomingDto) => existing.Update(incomingDto.Street, incomingDto.City));
    // removeFunc omitted - unmatched addresses are kept
```

## Usage in Repository

```csharp
// In {Entity}RepositoryTrxn - delegates to DbContext extension method:
public DomainResult<{Entity}> UpdateFromDto(
    {Entity} entity, {Entity}Dto dto,
    RelatedDeleteBehavior relatedDeleteBehavior = RelatedDeleteBehavior.None)
{
    return DB.UpdateFromDto(entity, dto, relatedDeleteBehavior);
}
```

## Usage in Service (via .Bind chaining)

```csharp
// CreateAsync - chain entity creation with child sync:
var result = dto.ToEntity(tenantId)
    .Bind(entity => repoTrxn.UpdateFromDto(entity, dto));

// UpdateAsync - after updating scalar properties:
var syncResult = repoTrxn.UpdateFromDto(entity, dto);
if (syncResult.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(syncResult.Errors);
```

## DomainResult Inheritance

`DomainResult<T>` inherits from `DomainResult`. This is critical for understanding callback compatibility:

- Domain factory methods (`Entity.Create(...)`, `entity.Update(...)`) return `DomainResult<T>`
- `SyncCollectionWithResult` callbacks expect `Func<TDto, DomainResult>` and `Func<TEntity, TDto, DomainResult>`
- Because `DomainResult<T> : DomainResult`, the factory/update return values satisfy these callback types without casting
- Error aggregation: `DomainResult.Combine(results.ToArray())` merges all errors from an array of `DomainResult` (or `DomainResult<T>`) into one
- Access combined errors via `combined.Errors` (returns `List<DomainError>`)

## Notes

- Updater is a **static extension method on `{Project}DbContextTrxn`** - gives access to `db.Delete()` for EF change-tracker removal
- Repository delegates via `DB.UpdateFromDto(entity, dto, relatedDeleteBehavior)` where `DB` is the `RepositoryBase` context property
- Uses railway `.Bind()` flow: `entity.Update(...).Bind(updatedEntity => DomainResult.Combine(...).Map(updatedEntity))` - parent update errors short-circuit child syncs
- `RelatedDeleteBehavior` gates whether `removeFunc` actually deletes: `None` = no-op, `RelationshipOnly` / `RelationshipAndEntity` = `db.Delete(toRemove)` + collection remove
- **CRITICAL:** Must call `db.Delete(toRemove)` in removeFunc, not just `collection.Remove()` - without explicit EF delete, orphaned children remain in DB when relationship isn't cascade-delete
- `dto.{ChildEntity}s ?? []` - null-coalesce to empty array so `SyncCollectionWithResult` gets a valid collection (null DTO collection = no changes, empty = remove all)
- `CollectionUtility.SyncCollectionWithResult` handles error aggregation internally via `DomainResult.Combine()`
- All callbacks return `DomainResult` (not void) - even remove must return `DomainResult.Success()`
- `DomainResult<T>` inherits from `DomainResult` - domain factory methods (`Create`, `Update`) return `DomainResult<T>` which satisfies the `Func<TDto, DomainResult>` parameter
- Uses the entity's domain methods (`Create`, `Update`) - never mutates properties directly
- `createFunc` must add the new entity to the parent's navigation collection - `SyncCollectionWithResult` does not do this automatically
- `getDtoId` returns `TId?` - null or default(TId) signals a new item (create path)
- Explicit generic type args (`<TEntity, TDto, TId>`) recommended when lambdas make inference ambiguous
