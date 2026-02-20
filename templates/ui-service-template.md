# UI Business Service Template

| | |
|---|---|
| **Files** | `Business/Services/{Feature}/I{Entity}Service.cs`, `{Entity}Service.cs` |
| **Depends on** | [ui-model-template](ui-model-template.md) |
| **Referenced by** | [mvux-model-template](mvux-model-template.md), [uno-ui.md](../skills/uno-ui.md) |

## Service Interface

```csharp
namespace {Project}.UI.Business.Services.{Feature};

/// <summary>
/// Client-side service for {Entity} operations via the Gateway API.
/// </summary>
public interface I{Entity}Service
{
    /// <summary>Get all {entity}s.</summary>
    ValueTask<IImmutableList<{Entity}>> GetAll(CancellationToken ct);

    /// <summary>Get a single {entity} by ID.</summary>
    ValueTask<{Entity}> GetById(Guid id, CancellationToken ct);

    /// <summary>Create a new {entity}.</summary>
    ValueTask Create({Entity} entity, CancellationToken ct);

    /// <summary>Update an existing {entity}.</summary>
    ValueTask Update({Entity} entity, CancellationToken ct);

    /// <summary>Delete a {entity} by ID.</summary>
    ValueTask Delete(Guid id, CancellationToken ct);

    /// <summary>Toggle favorite status.</summary>
    ValueTask Favorite({Entity} entity, CancellationToken ct);

    // Add child collection methods as needed:
    // ValueTask<IImmutableList<{ChildEntity}>> Get{ChildEntity}s(Guid {entity}Id, CancellationToken ct);
}
```

## Service Implementation

```csharp
using {Project}.UI.Business.Models;
using {Project}.UI.Client;
using {Project}.UI.Presentation.Messages;

namespace {Project}.UI.Business.Services.{Feature};

/// <summary>
/// Calls the Gateway API via Kiota-generated client.
/// Maps wire DTOs to client-side records.
/// Sends EntityMessage on mutations for MVUX auto-refresh.
/// </summary>
public class {Entity}Service(
    {Project}ApiClient api,
    IMessenger messenger) : I{Entity}Service
{
    public async ValueTask<IImmutableList<{Entity}>> GetAll(CancellationToken ct)
    {
        var data = await api.Api.{Entity}.GetAsync(cancellationToken: ct);
        return data?.Select(d => new {Entity}(d)).ToImmutableList()
            ?? ImmutableList<{Entity}>.Empty;
    }

    public async ValueTask<{Entity}> GetById(Guid id, CancellationToken ct)
    {
        var data = await api.Api.{Entity}[id].GetAsync(cancellationToken: ct);
        return new {Entity}(data!);
    }

    public async ValueTask Create({Entity} entity, CancellationToken ct)
    {
        await api.Api.{Entity}.PostAsync(entity.ToData(), cancellationToken: ct);
        messenger.Send(new EntityMessage<{Entity}>(EntityChange.Created, entity));
    }

    public async ValueTask Update({Entity} entity, CancellationToken ct)
    {
        await api.Api.{Entity}[entity.Id].PutAsync(entity.ToData(), cancellationToken: ct);
        messenger.Send(new EntityMessage<{Entity}>(EntityChange.Updated, entity));
    }

    public async ValueTask Delete(Guid id, CancellationToken ct)
    {
        await api.Api.{Entity}[id].DeleteAsync(cancellationToken: ct);
        messenger.Send(new EntityMessage<{Entity}>(EntityChange.Deleted, new {Entity} { Id = id }));
    }

    public async ValueTask Favorite({Entity} entity, CancellationToken ct)
    {
        var updated = entity with { IsFavorite = !entity.IsFavorite };
        await api.Api.{Entity}.Favorited.PostAsync(q =>
        {
            q.QueryParameters.{Entity}Id = updated.Id;
        }, cancellationToken: ct);
        messenger.Send(new EntityMessage<{Entity}>(EntityChange.Updated, updated));
    }
}
```

## Rules

- Use **primary constructor** injection (C# 12+)
- All methods return `ValueTask` or `ValueTask<T>`
- Always accept `CancellationToken ct` as the last parameter
- Return `IImmutableList<T>` (never `List<T>` or `IEnumerable<T>`)
- Map Kiota wire DTOs → client records via constructor: `new {Entity}(data)`
- Map client records → wire DTOs via `entity.ToData()` for POST/PUT
- Send `EntityMessage<T>` via `IMessenger` after every mutation (create, update, delete)
- Register as singleton in `App.xaml.host.cs` → `ConfigureServices`
