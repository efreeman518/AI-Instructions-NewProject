# Package Dependencies (EF.*)

Use this file as a compact contract map for private/shared packages.

## Sources

- Internal packages source: <https://github.com/efreeman518/EF.Packages>
- Reference usage: `sampleapp/src/Directory.Packages.props`, `sampleapp/src/nuget.config`

## Feed + Version Rules (Mandatory)

1. `nuget.config` must include `nuget.org` and all `customNugetFeeds` from [../domain-inputs.schema.md](../domain-inputs.schema.md).
2. Use central package versions in `Directory.Packages.props`.
3. After adding packages, restore and update to latest stable versions.
4. Re-verify with `dotnet restore` and `dotnet build`.

---

## Critical Domain Contracts

### `EF.Domain`

```csharp
public interface IEntityBase<TId> { TId Id { get; init; } }
```

```csharp
public abstract class EntityBase : IEntityBase<Guid>
{
    public Guid Id { get; init; }   // uses Guid.CreateVersion7()
    public byte[]? RowVersion { get; set; }
}
```

```csharp
public abstract class AuditableBase<TAuditIdType> : EntityBase
{
    public DateTime CreatedDate { get; set; }
    public TAuditIdType CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public TAuditIdType? UpdatedBy { get; set; }
}
```

```csharp
public interface ITenantEntity<TTenantIdType> where TTenantIdType : struct
{
    TTenantIdType TenantId { get; init; }
}
```

Critical invariants:
- Entity IDs use `Guid.CreateVersion7()`.
- Keep optimistic concurrency via `RowVersion`.
- Tenant entities implement `ITenantEntity<T>`.

### `EF.Domain.Contracts`

```csharp
public record DomainError(string Error, string? Code = null);
```

`DomainResult` / `DomainResult<T>`:
- success/failure states
- `Errors: IReadOnlyList<DomainError>`
- map/bind/match/tap helpers for railway flow

Also available:
- `IEntityBaseDto` (`Guid? Id`)
- `DomainException`
- `[Mask]` attribute for redaction

---

## Data Layer Contracts

### `EF.Data`

`DbContextBase<TAuditIdType, TTenantIdType>` provides:
- audit/tenant context fields
- tenant filter helpers
- concurrency-aware `SaveChangesAsync(OptimisticConcurrencyWinner winner, ...)`

`EntityBaseConfiguration<T>` standardizes:
- key mapping (`Id`)
- `ValueGeneratedNever()`
- row-version configuration

### `EF.Data.Contracts`

`IRepositoryBase` supports:
- CRUD (`Create`, `PrepareForUpdate`, `UpdateFull`, `Delete`, `DeleteAsync`)
- existence and key lookups
- single query and paged queries
- projection queries and async streaming
- save changes with/without concurrency winner
- include/split-query options

Other key types:
- `OptimisticConcurrencyWinner` (`ClientWins`, `DBWins`, `Throw`)
- `SplitQueryThresholdOptions`
- `AuditInterceptor`
- `DbContextScopedFactory`
- queryable helpers (`IQueryableExtensions`)

---

## Common Contracts

### `EF.Common.Contracts`

- `Result` / `Result<T>` (same shape as domain result, but `Errors: IReadOnlyList<string>`)
- `IRequestContext<TAuditIdType, TTenantIdType>`
- `RequestContext<...>` implementation
- `PagedResponse<T>`
- `SearchRequest<TFilter>`, `Sort`, `SortOrder`
- `IMessage`
- `AuditEntry<TAuditIdType>` + `AuditStatus`

### `EF.Common`

- `ResultExtensions.ToResult(...)` for domain→application conversion
- expression/predicate helpers for EF-safe composition
- `CollectionUtility` (non-domain sync helpers)
- `NotFoundException`

---

## Background and Messaging

### `EF.BackgroundServices`

```csharp
public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, ValueTask> workItem);
    ValueTask QueueScopedBackgroundWorkItem<TScoped>(Func<TScoped, CancellationToken, ValueTask> workItem) where TScoped : notnull;
    ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
}
```

### `EF.BackgroundServices.InternalMessageBus`

```csharp
public interface IInternalMessageBus
{
    void AutoRegisterHandlers(IServiceProvider serviceProvider, params Assembly[] assemblies);
    void RegisterMessageHandler<T>(IMessageHandler<T> handler) where T : IMessage;
    Task Publish<T>(InternalMessageBusProcessMode mode, ICollection<T> messages, CancellationToken cancellationToken = default) where T : IMessage;
}

public interface IMessageHandler<in T> where T : IMessage
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
```

**Namespace warning:** `IInternalMessageBus` / `IMessageHandler<T>` come from `EF.BackgroundServices.InternalMessageBus`.

### `EF.Messaging`

Core abstractions used by scaffolding:
- `IServiceBusSender`
- `IEventGridPublisher`
- `IEventHubProducer`

Use package base classes for sender/publisher/processor implementations.

---

## Data Service Extensions

### `EF.CosmosDb`

- Base entity: `CosmosDbEntity` (`PartitionKey`, `id` alias)
- Main abstraction: `ICosmosDbRepository`
  - save/get/delete
  - paged query + projection
  - stream query variants

### `EF.Storage`

`IBlobRepository` supports:
- upload/download/delete/exists
- SAS URI generation
- container client retrieval
- blob leasing helpers

### `EF.Table`

`ITableRepository` supports:
- get/upsert/delete entity
- paged query and streaming

### `EF.KeyVault`

- `IKeyVaultManager`: secrets/keys/certs operations
- `IKeyVaultCryptoUtility`: encrypt/decrypt helpers

### `EF.Grpc`

Provides interceptors and registration helpers for consistent gRPC error handling.

### `EF.FilterBuilder`

`FilterSet` / `Filter` contracts to generate dynamic query filters.

---

## Public Packages Used with EF.*

- `Refit.HttpClientFactory`
- `Microsoft.Extensions.Http.Resilience`

Pattern reference: [external-api.md](external-api.md)

---

## Generation Checklist

- [ ] `nuget.config` includes `nuget.org` + all custom feeds
- [ ] `Directory.Packages.props` owns versions
- [ ] `global.json` pins SDK with roll-forward policy
- [ ] Correct package placement across Domain/Data/Application/Infrastructure hosts
- [ ] Latest stable versions resolved successfully
- [ ] `EntityBase.Id` behavior preserved (`Guid.CreateVersion7()`)
- [ ] Domain/application error types are not mixed:
  - `DomainResult.Errors` → `IReadOnlyList<DomainError>`
  - `Result.Errors` → `IReadOnlyList<string>`
- [ ] Internal message bus namespaces are correct
- [ ] Azure client factories and package-required DI wiring are registered