# Data Persistence (EF Core)

## Overview

Use EF Core with split read/write contexts, explicit entity configurations, repository abstractions, updater helpers for aggregate child synchronization, and a disciplined migration strategy. Cosmos, Table Storage, and Blob Storage do not use EF migrations — see [azure-data-storage.md](azure-data-storage.md) for Cosmos schema evolution patterns.

Reference patterns: [../patterns/data-layer-wiring.md](../patterns/data-layer-wiring.md) (Database Context Pooling, DbContext OnModelCreating Order).
Base types (`DbContextBase`, `RepositoryBase`, `AuditInterceptor`, `SearchRequest`/`SearchResponse`): [../support/ef-packages-reference.md](../support/ef-packages-reference.md) — do not regenerate these.

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

### Design-Time Factory

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

Updater rules: keep update chains in `Bind` flow, centralize create/update/remove in one sync call, use `RelatedDeleteBehavior` for relationship semantics.

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

### JSON Column Mapping (`ToJson()`) Troubleshooting

`ToJson()` with owned types is the preferred pattern for structured data stored as JSON in SQL Server. However, EF Core may fail to generate migrations for complex value-object graphs with **nested collections** or **dictionaries**.

**Fallback:** When `ToJson()` owned mappings fail during migration generation, use a serializer-backed value conversion to `nvarchar(max)` with a custom `ValueComparer`:

```csharp
builder.Property(e => e.ComplexData)
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<ComplexType>(v, (JsonSerializerOptions?)null)!)
    .HasColumnType("nvarchar(max)")
    .Metadata.SetValueComparer(
        new ValueComparer<ComplexType>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<ComplexType>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
```

> **Document any deviation:** If serialized JSON conversions are used instead of `ToJson()`, record this in `HANDOFF.md` and the repo docs so future sessions know to revisit when EF Core improves owned-type migration support.

Do **not** hand-edit generated migration files to work around `ToJson()` failures -- the model snapshot will be inconsistent.

### Configuration Rules

1. Keep PK non-clustered when clustered multi-tenant access index is used.
2. Use explicit table names (class-name aligned).
3. Set delete behavior explicitly (`Restrict` for references, `Cascade` for owned children).
4. Name indexes predictably (`IX_...` / `CIX_...`).
5. Set `HasMaxLength(N)` for strings (avoid `nvarchar(max)`).
6. Use default decimal precision; override only when domain requires it.
7. Use `datetime2` for `DateTime` columns.

---

## Migrations

### EF CLI Prerequisites

Before running any migration command, ensure `dotnet ef` is available. If not installed globally, set up repo-local tooling:

```powershell
dotnet ef --version                    # check availability
dotnet new tool-manifest               # if .config/dotnet-tools.json does not exist
dotnet tool install dotnet-ef           # install as local tool
```

> **Package source mapping:** If the project uses `nuget.config` with `<packageSourceMapping>`, add an explicit entry for `dotnet-ef`:
> ```xml
> <packageSource key="nuget.org">
>   <package pattern="dotnet-ef" />
> </packageSource>
> ```
> Without this, `dotnet tool install dotnet-ef` fails with NU1100.
>
> When local tooling is introduced, record that choice in `HANDOFF.md` so subsequent sessions know to use `dotnet tool restore` before running migrations.

### Migration Naming

Format: `YYYYMMDD_Description` -- one migration per feature/slice.

```
20260301_InitialCreate
20260305_AddCategoryColorHex
20260310_AddTodoItemPriority
20260315_RemoveDeprecatedStatusColumn
```

> **CRITICAL:** Never rename a migration after sharing it with any environment or team member. EF tracks migrations by name in `__EFMigrationsHistory`.

### Canonical CLI Commands

```powershell
# Set connection string
$env:EFCORETOOLSDB = "Server=..."

# Add migration
dotnet ef migrations add {MigrationName} `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn

# Apply to local database
dotnet ef database update `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn

# Generate idempotent script (for production deployments)
dotnet ef migrations script --idempotent `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn `
  -o migrations.sql
```

### Data Migrations

Migration files should contain **schema changes only** -- `CreateTable`, `AddColumn`, `AlterColumn`, `DropColumn`, etc.

For data transformations, use a one-time background job with idempotency guard:

```csharp
// One-time data migration job
public class BackfillCategoryColorHandler : IScheduledJobHandler
{
    public async Task ExecuteAsync(IServiceProvider sp, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<ICategoryRepositoryTrxn>();
        var unset = await repo.GetCategoriesWithoutColorAsync(ct);
        foreach (var category in unset)
        {
            category.Update(colorHex: "#808080"); // default gray
        }
        await repo.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
    }
}
```

Use `migrationBuilder.Sql()` **only** for simple, safe data operations (e.g., setting a non-null default on a new column):

```csharp
migrationBuilder.Sql("UPDATE Categories SET ColorHex = '#808080' WHERE ColorHex IS NULL");
```

### Zero-Downtime Schema Changes

Use the **expand/contract** pattern for breaking schema changes:

**Phase 1 -- Expand:**
Add new column (nullable or with default). Deploy code that **writes to both** old and new columns.

```csharp
// Migration: 20260310_AddNewStatusColumn
migrationBuilder.AddColumn<string>("StatusV2", "TodoItems", nullable: true);
```

**Phase 2 -- Backfill:**
Run data migration to populate new column from old column values.

```csharp
// Migration: 20260312_BackfillStatusV2
migrationBuilder.Sql("UPDATE TodoItems SET StatusV2 = Status WHERE StatusV2 IS NULL");
```

**Phase 3 -- Contract:**
Remove old column. Deploy code that **reads only** from new column.

```csharp
// Migration: 20260315_RemoveOldStatusColumn
migrationBuilder.DropColumn("Status", "TodoItems");
```

> Each phase is a **separate migration + deployment cycle**. Never combine expand and contract in one deployment.

### Rollback Strategy

**Development:**

```powershell
dotnet ef database update {PreviousMigrationName} `
  --context {App}DbContextTrxn
```

**Production:**

- Use **idempotent scripts** (`--idempotent`) for forward-only deployment
- Blue-green deploy: apply migration to new slot, verify, swap traffic
- Never delete a migration that has been applied to any shared environment

### Multi-Store Consistency

For solutions using SQL + Cosmos/Table hybrids:

1. **Migrations apply to SQL only** -- EF migrations manage SQL Server schema
2. **Cosmos schema changes are code-level** -- add property to C# model + set default value for existing documents
3. **Coordination workflow:**
   - Deploy code first (handles both old and new document shapes via default values)
   - Then run SQL migration
   - Document stores are schema-flexible -- no separate migration step needed

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

### Database Seeding / Reference Data

Use the `IStartupTask` pattern for idempotent seed data. Seeding runs after `app.Build()` and before `app.RunAsync()`:

```csharp
public class SeedReferenceDataTask : IStartupTask
{
    private readonly IDbContextFactory<{App}DbContextTrxn> _factory;

    public SeedReferenceDataTask(IDbContextFactory<{App}DbContextTrxn> factory)
        => _factory = factory;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var existing = (await db.Set<{Entity}>()
            .Where(e => e.IsSystem)
            .Select(e => e.Id)
            .ToListAsync(ct)).ToHashSet();

        var seeds = {Entity}Seeds.All
            .Where(s => !existing.Contains(s.Id));

        foreach (var seed in seeds)
        {
            db.Set<{Entity}>().Add(seed);
        }

        // Seed tasks bypass the concurrency guard -- use the bool overload
        await db.SaveChangesAsync(true, ct);
    }
}
```

**Seeding rules:**

- **Idempotent upserts:** Always check existence before inserting. Use `ToListAsync().ToHashSet()` + filter pattern above.
- **Seeding SaveChanges:** Seed tasks run at startup with no contention, so call `SaveChangesAsync(true, ct)` (the `bool acceptAllChangesOnSuccess` overload) to bypass the concurrency guard that `DbContextBase.SaveChangesAsync(CancellationToken)` enforces.
- **Static seed definitions:** Define seed data in a static class (e.g., `{Entity}Seeds.All`) -- never generate random IDs at runtime.
- **Use deterministic GUIDs:** Seed entities must use fixed `Guid` values so they are stable across environments.
- **System flag:** Mark seeded rows with `IsSystem = true` to prevent user deletion.
- **Registration:** Register in bootstrapper: `services.AddTransient<IStartupTask, SeedReferenceDataTask>();`
- **Order:** If multiple seed tasks exist, register them in dependency order (parent entities before children).

---

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
