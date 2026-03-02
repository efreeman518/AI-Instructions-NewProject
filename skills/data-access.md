# Data Access

## Purpose

Use EF Core with split read/write contexts, explicit entity configurations, repository abstractions, and updater helpers for aggregate child synchronization.

## Non-Negotiables

1. Keep separate transactional and query DbContexts.
2. Keep shared schema/model conventions in a base DbContext.
3. Use explicit `IEntityTypeConfiguration<T>` per entity.
4. Separate write repositories from read/projection repositories.
5. Use updater pattern for child collection sync (create/update/remove in one pass).

Reference implementation: `sample-app/src/Infrastructure/TaskFlow.Infrastructure.Data/` and `...Infrastructure.Repositories/`.

---

## DbContext Split Pattern

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

---

## Entity Configuration Pattern

Base configuration (CRITICAL — must exist in every project):

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

## Repository Pattern

See [repository-template.md](../templates/repository-template.md) for write/query repository implementations and interfaces.

Key rules:
- Write repo: `{Entity}RepositoryTrxn` with includes and `UpdateFromDto` delegation.
- Query repo: `{Entity}RepositoryQuery` with paged search using EF-safe projector expressions.
- Use transactional repo for writes, query repo for read/projection.

---

## Updater Pattern

See [updater-template.md](../templates/updater-template.md) for full implementation.

> **Delegation pattern:** The updater is an extension on `DbContextTrxn`, but services call it through the repository. The repository wraps the call: `DB.UpdateFromDto(entity, dto, relatedDeleteBehavior)` where `DB` is the DbContext property inherited from `RepositoryBase`.

Updater rules: keep update chains in `Bind` flow, centralize create/update/remove in one sync call, use `RelatedDeleteBehavior` for relationship semantics.

---

## SaveChangesAsync — Critical Rule

`DbContextBase.SaveChangesAsync(CancellationToken)` **ALWAYS throws `NotImplementedException`** by design. The 1-param overload is intentionally blocked to force use of the concurrency-safe path.

```csharp
// ✅ CORRECT — always use the 2-param overload
await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

// ❌ WRONG — throws NotImplementedException at runtime
await repoTrxn.SaveChangesAsync(ct);
```

The 2-param overload retries on `DbUpdateConcurrencyException` using either client-wins or database-wins strategy.

> **Important:** `OptimisticConcurrencyWinner` is in `EF.Data.Contracts`. Add `global using EF.Data.Contracts;` to `Application.Services/GlobalUsings.cs`.

---

## Delete Pattern — Critical Rule

`Delete(entity)` inherited from `RepositoryBase` marks the entity for deletion in the change tracker. **You MUST call it before `SaveChangesAsync`** — simply loading an entity and saving will NOT delete it.

```csharp
var entity = await repoTrxn.GetAsync(id, false, ct);
if (entity == null) return Result.Success(); // idempotent
repoTrxn.Delete(entity);                     // marks for deletion
await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
```

---

## Design-Time Factory and Migrations

Use `IDesignTimeDbContextFactory<{Project}DbContextTrxn>` for CLI migrations.

```csharp
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<{Project}DbContextTrxn>
{
    public {Project}DbContextTrxn CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EFCORETOOLSDB")
            ?? throw new InvalidOperationException("Set EFCORETOOLSDB env var");

        var optionsBuilder = new DbContextOptionsBuilder<{Project}DbContextTrxn>();
        optionsBuilder.UseSqlServer(connectionString);
        return new {Project}DbContextTrxn(optionsBuilder.Options);
    }
}
```

Common commands:

```powershell
$env:EFCORETOOLSDB = "Server=..."
dotnet ef migrations add {MigrationName} --context {Project}DbContextTrxn --project Infrastructure.Data
dotnet ef migrations script --idempotent --context {Project}DbContextTrxn --project Infrastructure.Data
dotnet ef database update --context {Project}DbContextTrxn --project Infrastructure.Data
```

---

## Bootstrapper Alignment

Keep full registration details in [bootstrapper.md](bootstrapper.md):

- pooled DbContext factories,
- audit interceptor on transactional context,
- no-tracking and read optimizations on query context,
- retry and provider options.

---

## SearchRequest Defaults (Critical)

See [test-gotchas.md](../test-gotchas.md) for the canonical paging defaults guidance and test/runtime failure patterns.

---

## Verification

- [ ] both `{App}DbContextTrxn` and `{App}DbContextQuery` exist
- [ ] query context is configured for no-tracking reads
- [ ] each entity has explicit `IEntityTypeConfiguration<T>` inheriting `EntityBaseConfiguration<T>`
- [ ] `EntityBaseConfiguration<T>` configures `HasKey`, `ValueGeneratedNever`, `IsRowVersion()`
- [ ] repositories are split for write and read concerns
- [ ] read queries use projector expressions
- [ ] update paths use updater sync pattern for child collections
- [ ] migrations run through transactional context with design-time factory
- [ ] mappings/repositories align with [entity-template.md](../templates/entity-template.md) and [repository-template.md](../templates/repository-template.md)