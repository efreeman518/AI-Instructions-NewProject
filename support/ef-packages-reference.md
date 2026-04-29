# EF.Packages — Shared Library Reference

The scaffolded project depends on the **EF.Packages** private NuGet feed for infrastructure, patterns, and abstractions. These types are shared-library types. Do not regenerate them.

> **NuGet feed:** Configure it in `nuget.config` before Phase 4. Local environments need a GitHub PAT exposed through `NUGET_AUTH_TOKEN` or equivalent credential provider. Verify with `dotnet restore` (exit code 0) in Phase 3 and after Phase 4 build. See [execution-gates.md](execution-gates.md).

---

## Key Types by Layer

These types are consumed throughout scaffolded code. Know where they come from so you don't recreate them.

> **Verified against EF.Packages v1.0.58.** If a type is not listed here, it either does not exist in EF.Packages or has not been verified. Check the actual assemblies before assuming a type exists.

### Domain Layer (EF.Domain, EF.Domain.Contracts)

| Type | Package | Used For |
|---|---|---|
| `EntityBase` | EF.Domain | Base class for all domain entities (Id, audit fields) |
| `AuditableBase<TAuditIdType>` | EF.Domain | Base class with audit trail fields |
| `CollectionUtility` | EF.Domain | Utility for collection operations |
| `IEntityBase<TKey>` | EF.Domain.Contracts | Entity base interface |
| `ITenantEntity<TTenant>` | EF.Domain.Contracts | Tenant-scoped entity marker; enables global query filters |
| `IAuditable<TAuditIdType>` | EF.Domain.Contracts | Audit trail interface (CreatedBy, ModifiedBy, timestamps) |
| `DomainResult<T>` | EF.Domain.Contracts | Railway-style result for domain operations (Success/Failure) |
| `DomainResult` | EF.Domain.Contracts | Non-generic domain result |
| `DomainError` | EF.Domain.Contracts | Typed error with code + message for domain validation |

### Data Access Layer (EF.Data, EF.Data.Contracts)

| Type | Package | Used For |
|---|---|---|
| `DbContextBase<TAuditIdType, TTenantIdType>` | EF.Data | Base DbContext with tenant filters, audit interceptor hooks, default type config |
| `RepositoryBase<TDbContext, TAuditIdType, TTenantIdType>` | EF.Data | Generic repository with CRUD, paging, search. Members: Create, Delete, DeleteAsync, ExistsAsync, GetEntityAsync, GetEntityByKeysAsync, GetEntityProjectionAsync, PrepareForUpdate, QueryPageAsync, QueryPageProjectionAsync, SaveChangesAsync, UpdateFull, UpsertAsync |
| `IRepositoryBase` | EF.Data.Contracts | Base repository interface (non-generic) |
| `AuditInterceptor<TAuditIdType, TTenantIdType>` | EF.Data | EF SaveChanges interceptor for audit field population; on `SavedChangesAsync` it publishes collected audit entries through `IInternalMessageBus` |
| `ConnectionNoLockInterceptor` | EF.Data | Adds `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` for query contexts |
| `ReadUncommittedInterceptor` | EF.Data | Read uncommitted isolation level interceptor |
| `DbContextScopedFactory<TContext, TAuditIdType, TTenantIdType>` | EF.Data | Scoped wrapper around `IDbContextFactory<T>` for DI resolution |
| `OptimisticConcurrencyWinner` | EF.Data.Contracts | Enum for SaveChangesAsync conflict resolution strategy |
| `IQueryableExtensions` | EF.Data.Contracts | Extension methods for IQueryable |
| `AuditChangeAttribute` | EF.Data.Contracts | Attribute for audit change tracking |
| `RelatedDeleteBehavior` | EF.Data.Contracts | Enum for related entity delete behavior |
| `MigrationSupport` | EF.Data | Migration support utilities |
| `ResilientTransaction` | EF.Data | Resilient transaction wrapper |

### Common Infrastructure (EF.Common, EF.Common.Contracts)

| Type | Package | Used For |
|---|---|---|
| `IRequestContext<TUser, TTenant>` | EF.Common.Contracts | Scoped request context (CorrelationId, AuditId, TenantId, Roles, RoleExists()) |
| `RequestContext<TUser, TTenant>` | EF.Common.Contracts | Default implementation of IRequestContext. Constructor order: `(correlationId, auditId, tenantId, roles)` |
| `Result<T>` | EF.Common.Contracts | Application-layer result wrapper (Success/Failure/None). Members: IsSuccess, IsFailure, IsNone, Value, ErrorMessage, Errors, Match, Map, Bind, BindOrContinue, OnSuccess, OnFailure, Tap. **Not JSON-deserializable** — lacks parameterless constructor; use `JsonDocument` parsing in tests. When passed to `Results.Ok(result)` in endpoints, serializes to just the `Value` payload (not the full Result wrapper). |
| `Result` | EF.Common.Contracts | Non-generic result (Success/Failure). Members: IsSuccess, IsFailure, Combine, Match, Map |
| `PagedResponse<T>` | EF.Common.Contracts | Paged response with Data, Total, PageSize, PageIndex |
| `SearchRequest<TFilter>` | EF.Common.Contracts | Paged search request with PageSize, PageIndex, Sorts, Filter |
| `Sort` | EF.Common.Contracts | Sort descriptor (PropertyName, SortOrder) |
| `SortOrder` | EF.Common.Contracts | Enum: Ascending=0, Descending=1 |
| `IMessage` | EF.Common.Contracts | Marker interface for domain events/messages |
| `ISpecification<T>` / `Specification<T>` | EF.Common.Contracts | Specification pattern base |
| `StaticItem<TKey, TTenantKey>` | EF.Common.Contracts | Lookup item for dropdowns |
| `StaticList<T>` | EF.Common.Contracts | Typed collection for lookup data |
| `StaticData` | EF.Common.Contracts | Static data container |
| `AuditEntry<TAuditIdType, TTenantIdType>` | EF.Common.Contracts | Audit entry record carrying both audit identity and tenant identity |
| `AuditStatus` | EF.Common.Contracts | Audit status enum |
| `StaticLogging` | EF.Common | Pre-host logger factory for startup/shutdown logging |
| `PredicateBuilder` | EF.Common | Dynamic LINQ predicate builder |

### Background Services (EF.BackgroundServices)

| Type | Package | Used For |
|---|---|---|
| `IInternalMessageBus` | EF.BackgroundServices | In-process message bus for domain event dispatch |
| `InternalMessageBus` | EF.BackgroundServices | Default implementation of IInternalMessageBus; dispatches through `IBackgroundTaskQueue`, not inline |
| `IMessageHandler<T>` | EF.BackgroundServices | Handler interface for messages (where T : IMessage) |
| `ScopedMessageHandlerAttribute` | EF.BackgroundServices | Attribute for scoped message handler discovery |
| `InternalMessageBusProcessMode` | EF.BackgroundServices | Dispatch mode for in-process bus fan-out / queue semantics |
| `CronBackgroundService<T>` | EF.BackgroundServices | Cron-scheduled background service |
| `ICronJobHandler<T>` | EF.BackgroundServices | Handler interface for cron jobs |
| `IBackgroundTaskQueue` | EF.BackgroundServices | Queue for background task processing |
| `ScopedBackgroundService` | EF.BackgroundServices | Base class for scoped background services |

#### Critical Wiring Notes

- `AuditInterceptor` does **not** persist audit rows itself. It publishes `AuditEntry<...>` messages through `IInternalMessageBus` after `SaveChangesAsync` succeeds.
- `InternalMessageBus.Publish(...)` is a synchronous fire-and-forget API over the channel queue. Do not invent `PublishAsync` or single-message overloads.
- `InternalMessageBus` depends on the channel-based `IBackgroundTaskQueue`; if the queue/hosted service is missing, `Publish(...)` succeeds but handlers never run.
- `[ScopedMessageHandler]` controls handler scope during dispatch only. Handlers still must be registered in DI and then wired into the bus after host build.

### Application Host (EF.Host, EF.AspNetCore)

| Type | Package | Used For |
|---|---|---|
| `IConfigurationBuilderExtensions` | EF.Host | Configuration builder extensions |
| `IHostApplicationBuilderExtensions` | EF.Host | Host builder extensions (includes RunStartupTasks) |
| `DefaultExceptionHandler` | EF.AspNetCore | Base `IExceptionHandler` for ProblemDetails mapping (requires ASP.NET Core host) |

### Caching (EF.Cache)

| Type | Package | Used For |
|---|---|---|
| `CacheSettings` | EF.Cache | Config model bound from `CacheSettings[]` in appsettings |

### Testing (EF.Test.Unit, EF.Test.Integration)

| Type | Package | Used For |
|---|---|---|
| `UnitTestBase` | EF.Test.Unit | Base class for `Test.Unit` (mocked unit tests) |
| `IntegrationTestBase` | EF.Test.Integration | Base class with TestContainers + WebApplicationFactory; consumed by `Test.Endpoints`, `Test.E2E`, and `Test.Integration` |

**Naming asymmetry:** `EF.Test.Integration` is the EF package name and predates the project split; it provides the WebApplicationFactory base used by `Test.Endpoints` (per-endpoint contract), `Test.E2E` (workflow chains), and `Test.Integration` (service-level vs real external services). The package name is preserved for ecosystem compatibility.

---

## App-Level Types (NOT in EF.Packages)

These types appear in the service and endpoint templates but are **not provided by EF.Packages**. They must be created in the target project. Generate them during Phase 5b.

| Type | Where to Create | Used For |
|---|---|---|
| `DefaultRequest<T>` | Application.Models | Request wrapper for Create/Update service methods |
| `DefaultResponse<T>` | Application.Models | Response wrapper for Get/Create/Update service methods |
| `AppConstants` | Application.Contracts | Role names (ROLE_GLOBAL_ADMIN), cache names (DEFAULT_CACHE) |
| `ITenantBoundaryValidator` | Application.Contracts | Tenant boundary enforcement interface |
| `TenantBoundaryValidator` | Application.Services | Default implementation — GlobalAdmin bypass + tenant matching |
| `IEntityCacheProvider` | Application.Contracts | Abstraction for entity-level caching |
| `NoOpEntityCacheProvider` | Application.Services | No-op stub used until Phase 5c wires FusionCache |

> **Do not search EF.Packages for these types.** They are intentionally app-level to keep the shared library thin. The service template references them because every scaffolded project needs them.

---

## Phase Usage

- **5a:** EF.Common, EF.Domain, EF.Domain.Contracts, EF.Data, EF.Data.Contracts, EF.Common.Contracts
- **5b:** EF.AspNetCore, EF.Host, EF.FilterBuilder, EF.Cache, optional auth/key vault packages
- **5c:** EF.BackgroundServices and messaging packages when enabled
- **5d:** EF.Test.Unit and EF.Test.Integration
- **5e:** EF.Auth and optional EF.MSGraph; EF.AzureOpenAI or EF.OpenAI (when AI in scope)

---

## Rules

- **Never regenerate types that exist in EF.Packages.** Check this reference before creating base classes, result types, or repository interfaces.
- All EF.Packages target **.NET 10.0**.
- Packages use **central package management** — add versions to `Directory.Packages.props`.
- Private feed must be in `nuget.config` with `<packageSourceMapping>` entries for `EF.*` packages.
- Project files must not put `Version="..."` on `EF.*` `<PackageReference>` entries.
- If source contains local definitions for `EntityBase`, `RepositoryBase`, `DbContextBase`, `Result`, `PagedResponse`, `SearchRequest`, `IRequestContext`, `IInternalMessageBus`, or `IMessageHandler`, stop and replace them with package references.
