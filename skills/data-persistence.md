# Data Persistence (EF Core)

## Overview

Use EF Core with split read/write contexts, explicit entity configurations, repository abstractions, updater helpers for child synchronization, and concurrency-safe save paths.

Reference patterns: [../patterns/data-layer-wiring.md](../patterns/data-layer-wiring.md).
Base types (`DbContextBase`, `RepositoryBase`, `AuditInterceptor`, `SearchRequest`, `PagedResponse`): [../support/ef-packages-reference.md](../support/ef-packages-reference.md).

Load [../support/data-persistence-advanced.md](../support/data-persistence-advanced.md) only when the current task needs design-time factory setup, migrations, JSON column troubleshooting, startup seeding, or expand/contract guidance.

---

## DbContext Design

### Split Pattern

```csharp
public abstract class {Project}DbContextBase(DbContextOptions options)
    : DbContextBase<string, Guid?>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("{project}");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof({Project}DbContextBase).Assembly);
        ConfigureDefaultDataTypes(modelBuilder);
        SetTableNames(modelBuilder);
        ConfigureTenantQueryFilters(modelBuilder);
    }
}

public class {Project}DbContextTrxn(DbContextOptions<{Project}DbContextTrxn> options)
    : {Project}DbContextBase(options) { }

public class {Project}DbContextQuery(DbContextOptions<{Project}DbContextQuery> options)
    : {Project}DbContextBase(options) { }
```

Register query context with `NoTracking` behavior.

### Bootstrapper Alignment

Keep full registration details in [bootstrapper.md](bootstrapper.md):

- Pooled DbContext factories
- Audit interceptor on transactional context
- No-tracking and read optimizations on query context
- Retry and provider options

---

## Repository Pattern

See [repository-template.md](../templates/repository-template.md) for write/query repository implementations and interfaces.

Key rules:
- Write repo: `{Entity}RepositoryTrxn` with includes and `UpdateFromDto` delegation.
- Query repo: `{Entity}RepositoryQuery` with paged search using EF-safe projector expressions.
- Use transactional repo for writes, query repo for read/projection.

### Updater Pattern

See [updater-template.md](../templates/updater-template.md) for full implementation.

> **Delegation pattern:** The updater is an extension on `DbContextTrxn`, but services call it through the repository. The repository wraps the call: `DB.UpdateFromDto(entity, dto, relatedDeleteBehavior)` where `DB` is the DbContext property inherited from `RepositoryBase`.

Updater rules:

- Keep update chains in `Bind` flow.
- Centralize add/update/remove in one sync call.
- Use `RelatedDeleteBehavior` explicitly for relationship semantics.
- Keep collection diff logic out of services.

### SearchRequest Defaults (Critical)

See [troubleshooting.md](../support/troubleshooting.md) for the canonical paging defaults guidance and test/runtime failure patterns.

---

## Entity Configuration

Base configuration (CRITICAL -- must exist in every project):

```csharp
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

> **CRITICAL:** Every project must create this abstract base. ALL entity configurations MUST inherit from it. Without it, `RowVersion` won't function as a concurrency token, and `Id` may be auto-generated.

See [ef-configuration-template.md](../templates/ef-configuration-template.md) for entity-specific configuration patterns.

### Configuration Rules

1. Keep PK non-clustered when clustered multi-tenant access index is used.
2. Use explicit table names (class-name aligned).
3. Set delete behavior explicitly (`Restrict` for references, `Cascade` for owned children).
4. Name indexes predictably (`IX_...` / `CIX_...`).
5. Set `HasMaxLength(N)` for strings (avoid `nvarchar(max)`).
6. Use default decimal precision; override only when domain requires it.
7. Use `datetime2` for `DateTime` columns.

---

## Advanced Topics

Load [../support/data-persistence-advanced.md](../support/data-persistence-advanced.md) when the current task needs:

- design-time factory setup,
- EF CLI prerequisites or migration commands,
- `ToJson()` / JSON column troubleshooting,
- startup seeding patterns,
- expand/contract guidance, or
- multi-store schema coordination.

---

## SaveChangesAsync Rules

`DbContextBase.SaveChangesAsync(CancellationToken)` **ALWAYS throws `NotImplementedException`** by design. The 1-param overload is intentionally blocked to force use of the concurrency-safe path.

```csharp
// CORRECT -- always use the 2-param overload
await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

// WRONG -- throws NotImplementedException at runtime
await repoTrxn.SaveChangesAsync(ct);
```

The 2-param overload retries on `DbUpdateConcurrencyException` using either client-wins or database-wins strategy.

> **Important:** `OptimisticConcurrencyWinner` is in `EF.Data.Contracts`. Add `global using EF.Data.Contracts;` to `Application.Services/GlobalUsings.cs`.

### Delete Pattern

`Delete(entity)` inherited from `RepositoryBase` marks the entity for deletion in the change tracker. **You MUST call it before `SaveChangesAsync`** -- simply loading an entity and saving will NOT delete it.

```csharp
var entity = await repoTrxn.Get{Entity}Async(id, false, ct);
if (entity == null) return Result.Success(); // idempotent
repoTrxn.Delete(entity);                     // marks for deletion
await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
```

## Verification

- [ ] Both `{App}DbContextTrxn` and `{App}DbContextQuery` exist
- [ ] Query context is configured for no-tracking reads
- [ ] Each entity has explicit `IEntityTypeConfiguration<T>` inheriting `EntityBaseConfiguration<T>`
- [ ] `EntityBaseConfiguration<T>` configures `HasKey`, `ValueGeneratedNever`, `IsRowVersion()`
- [ ] Repositories are split for write and read concerns
- [ ] Read queries use projector expressions
- [ ] Update paths use updater sync pattern for child collections
- [ ] Design-time factory exists and uses `EFCORETOOLSDB` env var
- [ ] Migration name follows `YYYYMMDD_Description` format
- [ ] One migration per feature/slice -- no mega-migrations
- [ ] CLI commands use `--context {App}DbContextTrxn` (never query context)
- [ ] Data backfill uses background job (not inline migration SQL) for complex transforms
- [ ] Breaking schema changes use expand/contract across multiple deployments
- [ ] Production deployments use idempotent scripts
- [ ] No migration renamed after sharing
- [ ] Multi-store changes deploy code before SQL migration
- [ ] Mappings/repositories align with [entity-template.md](../templates/entity-template.md) and [repository-template.md](../templates/repository-template.md)
