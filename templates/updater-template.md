# Updater Template

| | |
|---|---|
| **File** | `Infrastructure.Repositories/Updaters/{Entity}Updater.cs` |
| **Depends on** | [entity-template](entity-template.md), [dto-template](dto-template.md) |
| **Referenced by** | [repository-template](repository-template.md) |

## File: Infrastructure/Repositories/Updaters/{Entity}Updater.cs

The updater pattern handles synchronizing a parent entity's child collections during Update operations — matching incoming DTOs against existing entities and performing add/remove/update accordingly.

```csharp
namespace Infrastructure.Repositories.Updaters;

internal static class {Entity}Updater
{
    /// <summary>
    /// Sync the entity's child collections from the incoming DTO.
    /// Returns a DomainResult with aggregated errors from all sync operations.
    /// </summary>
    public static DomainResult<{Entity}> Sync{Entity}({Entity} entity, {Entity}Dto dto)
    {
        var errors = new List<string>();

        // Sync {ChildEntity}s collection
        Sync{ChildEntity}s(entity, dto.{ChildEntity}s, errors);

        return errors.Count > 0
            ? DomainResult<{Entity}>.Failure(errors)
            : DomainResult<{Entity}>.Success(entity);
    }

    private static void Sync{ChildEntity}s(
        {Entity} entity,
        List<{ChildEntity}Dto> incoming,
        List<string> errors)
    {
        CollectionUtility.SyncCollectionWithResult(
            existing: entity.{ChildEntity}s,
            incoming: incoming,
            existingKey: e => e.Id,
            incomingKey: i => i.Id,
            update: (existingChild, incomingDto) =>
            {
                var result = existingChild.Update(incomingDto.Name);
                if (result.IsFailure) errors.Add(result.ErrorMessage!);
            },
            add: incomingDto =>
            {
                var createResult = {ChildEntity}.Create(incomingDto.Name);
                if (createResult.IsFailure)
                {
                    errors.Add(createResult.ErrorMessage!);
                    return null;
                }
                var addResult = entity.Add{ChildEntity}(createResult.Value!);
                if (addResult.IsFailure) errors.Add(addResult.ErrorMessage!);
                return createResult.Value;
            },
            remove: existingChild =>
            {
                var result = entity.Remove{ChildEntity}(existingChild.Id);
                if (result.IsFailure) errors.Add(result.ErrorMessage!);
            });
    }
}
```

## CollectionUtility.SyncCollectionWithResult

Generic utility for synchronizing two collections:

```csharp
namespace Package.Infrastructure.Common.Utilities;

public static class CollectionUtility
{
    /// <summary>
    /// Synchronize an existing collection with an incoming collection.
    /// Matches by key; adds new items, updates matching items, removes missing items.
    /// </summary>
    public static void SyncCollectionWithResult<TExisting, TIncoming, TKey>(
        IReadOnlyCollection<TExisting> existing,
        IReadOnlyCollection<TIncoming> incoming,
        Func<TExisting, TKey> existingKey,
        Func<TIncoming, TKey?> incomingKey,
        Action<TExisting, TIncoming> update,
        Func<TIncoming, TExisting?> add,
        Action<TExisting> remove)
        where TKey : struct
    {
        var existingDict = existing.ToDictionary(existingKey);
        var incomingKeys = incoming.Where(i => incomingKey(i).HasValue)
                                    .ToDictionary(i => incomingKey(i)!.Value);

        // Update existing items that are still present
        foreach (var item in existing)
        {
            var key = existingKey(item);
            if (incomingKeys.TryGetValue(key, out var match))
            {
                update(item, match);
            }
        }

        // Remove items no longer in incoming collection
        var toRemove = existing.Where(e => !incomingKeys.ContainsKey(existingKey(e))).ToList();
        foreach (var item in toRemove)
        {
            remove(item);
        }

        // Add new items (no key = new)
        var toAdd = incoming.Where(i => !incomingKey(i).HasValue || !existingDict.ContainsKey(incomingKey(i)!.Value));
        foreach (var item in toAdd)
        {
            add(item);
        }
    }
}
```

## Usage in Service

```csharp
// In {Entity}Service.UpdateAsync:
var syncResult = {Entity}Updater.Sync{Entity}(entity, dto);
if (syncResult.IsFailure)
    return Result<DefaultResponse<{Entity}Dto>>.Failure(syncResult.Errors);
```

## Notes

- Updater is a **static class** — no DI, no state
- Aggregates all errors across child sync operations and returns them together
- Uses the entity's domain methods (`Add{ChildEntity}`, `Remove{ChildEntity}`, `Update`) — never modifies EF navigation properties directly
- `CollectionUtility` handles the generic sync algorithm; entity-specific updaters handle the mapping
