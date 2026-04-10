# EF.Packages — Shared Library Reference

The scaffolded project depends on the **EF.Packages** private NuGet feed for infrastructure, patterns, and abstractions. These packages provide base types that the AI must **not regenerate** — use them directly.

> **NuGet feed:** Must be configured in `nuget.config` under `customNugetFeeds` before Phase 5. See [execution-gates.md](execution-gates.md) for preflight checks.

---

## Key Types by Layer

These types are consumed throughout scaffolded code. Know where they come from so you don't recreate them.

### Domain Layer (EF.Domain, EF.Domain.Contracts)

| Type | Package | Used For |
|---|---|---|
| `EntityBase<TKey>` | EF.Domain | Base class for all domain entities (Id, audit fields) |
| `ITenantEntity<TTenant>` | EF.Domain.Contracts | Tenant-scoped entity marker; enables global query filters |
| `DomainResult<T>` | EF.Domain.Contracts | Railway-style result for domain operations (Success/Failure) |
| `DomainError` | EF.Domain.Contracts | Typed error with code + message for domain validation |
| `IAuditableEntity` | EF.Domain.Contracts | Audit trail interface (CreatedBy, ModifiedBy, timestamps) |

### Data Access Layer (EF.Data, EF.Data.Contracts)

| Type | Package | Used For |
|---|---|---|
| `DbContextBase<TUser, TTenant>` | EF.Data | Base DbContext with tenant filters, audit interceptor hooks, default type config |
| `RepositoryBase<TEntity, TContext>` | EF.Data | Generic repository with CRUD, paging, search |
| `IRepositoryQuery<T>` / `IRepositoryTrxn<T>` | EF.Data.Contracts | Read/write repository interfaces |
| `AuditInterceptor<TUser, TTenant>` | EF.Data | EF SaveChanges interceptor for audit field population |
| `ConnectionNoLockInterceptor` | EF.Data | Adds `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` for query contexts |
| `DbContextScopedFactory<TContext, TUser, TTenant>` | EF.Data | Scoped wrapper around `IDbContextFactory<T>` for DI resolution |
| `SearchRequest<TFilter>` | EF.Data.Contracts | Paged search request (PageSize, PageIndex, Filter, Sort) |
| `SearchResponse<T>` | EF.Data.Contracts | Paged search response (Items, TotalCount, PageSize, PageIndex) |

### Common Infrastructure (EF.Common, EF.Common.Contracts)

| Type | Package | Used For |
|---|---|---|
| `IRequestContext<TUser, TTenant>` | EF.Common.Contracts | Scoped request context (CorrelationId, AuditId, TenantId, Roles) |
| `RequestContext<TUser, TTenant>` | EF.Common.Contracts | Default implementation of IRequestContext |
| `Result<T>` | EF.Common.Contracts | Application-layer result wrapper (Success/Failure/None) |
| `IStartupTask` | EF.Common.Contracts | Interface for host startup tasks (migrations, cache warming, seeding) |
| `OptimisticConcurrencyWinner` | EF.Common.Contracts | Enum for SaveChangesAsync conflict resolution strategy |

### Application Host (EF.Host, EF.AspNetCore)

| Type | Package | Used For |
|---|---|---|
| `StaticLogging` | EF.Host | Pre-host logger factory for startup/shutdown logging |
| `RunStartupTasks()` | EF.Host | Extension method that resolves and executes all `IStartupTask` implementations |
| `DefaultExceptionHandler` | EF.AspNetCore | Base `IExceptionHandler` for ProblemDetails mapping |

### Caching (EF.Cache)

| Type | Package | Used For |
|---|---|---|
| `CacheSettings` | EF.Cache | Config model bound from `CacheSettings[]` in appsettings |

### Testing (EF.Test.Unit, EF.Test.Integration)

| Type | Package | Used For |
|---|---|---|
| `UnitTestBase` | EF.Test.Unit | Base class for unit tests with service provider setup |
| `IntegrationTestBase` | EF.Test.Integration | Base class with TestContainers + WebApplicationFactory |

---

## Package Selection by Phase

| Phase | Packages to Reference |
|---|---|
| **5a (Foundation)** | EF.Common, EF.Domain, EF.Domain.Contracts, EF.Data, EF.Data.Contracts, EF.Common.Contracts |
| **5b (App Core)** | EF.AspNetCore, EF.Host, EF.FilterBuilder |
| **5c (Runtime/Edge)** | EF.Cache, EF.Auth (if auth configured), EF.KeyVault (if enabled) |
| **5d (Optional Hosts)** | EF.BackgroundService (scheduler), EF.Messaging (if events) |
| **5e (Quality)** | EF.Test.Unit, EF.Test.Integration |
| **5f (Auth)** | EF.Auth, EF.MSGraph (if MS Graph needed) |
| **5g (AI)** | EF.AzureOpenAI or EF.OpenAI |

---

## Quick Start by Scenario

| Scenario | Start With |
|---|---|
| Web API | EF.Common, EF.Domain.Contracts, EF.Domain, EF.Data, EF.AspNetCore |
| Cloud storage | EF.Storage (Blob), EF.CosmosDb (Document), EF.Table (Key-Value) |
| Messaging | EF.Messaging + EF.BackgroundService |
| AI features | EF.AzureOpenAI (enterprise) or EF.OpenAI (standard) |
| Auth | EF.Auth + EF.KeyVault |
| Full app | All of the above + EF.Cache, EF.Host, EF.Grpc |

---

## Rules

- **Never regenerate types that exist in EF.Packages.** Check this reference before creating base classes, result types, or repository interfaces.
- All EF.Packages target **.NET 10.0**.
- Packages use **central package management** — add versions to `Directory.Packages.props`.
- Private feed must be in `nuget.config` with `<packageSourceMapping>` entries for `EF.*` packages.
