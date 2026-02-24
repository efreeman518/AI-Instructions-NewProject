# Data Access

## Overview

Data access uses Entity Framework Core with a **split DbContext** pattern (read/write separation), **configuration classes** per entity, a **generic + specific repository** approach, and the **Updater pattern** for consistent child collection syncing.

## DbContext Split Pattern

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Data/` — `TaskFlowDbContextBase.cs` (abstract base with schema, tenant filters, data type defaults), `TaskFlowDbContextTrxn.cs` (writes), `TaskFlowDbContextQuery.cs` (reads, NoTracking).

### Base Context

Abstract base holds all model configuration, query filters, and DbSet declarations:

```csharp
// Compact pattern — see sampleapp for full implementation
public abstract class {Project}DbContextBase(DbContextOptions options) : DbContextBase<string, Guid?>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("{project}");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof({Project}DbContextBase).Assembly);
        ConfigureDefaultDataTypes(modelBuilder);
        SetTableNames(modelBuilder);  // class name = table name, no pluralization
        ConfigureTenantQueryFilters(modelBuilder);
    }
}
```

### Transactional (Writes) & Query (Reads) Contexts

```csharp
public class {Project}DbContextTrxn(DbContextOptions<{Project}DbContextTrxn> options) 
    : {Project}DbContextBase(options) { }

public class {Project}DbContextQuery(DbContextOptions<{Project}DbContextQuery> options) 
    : {Project}DbContextBase(options) { }  // registered with NoTracking
```

## Entity Configuration Pattern

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Data/Configurations/EntityConfigurations.cs` — consolidated configurations for all entities: TodoItem (rich entity with hierarchy, owned DateRange, composite indexes), Category (simple entity), TodoItemTag (junction with composite PK).

### Base Configuration

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

### Entity-Specific Configuration

```csharp
// Compact pattern — see sampleapp for full implementations
public class {Entity}Configuration() : EntityBaseConfiguration<{Entity}>(false)
{
    public override void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        base.Configure(builder);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Id })
               .HasDatabaseName("CIX_{Entity}_TenantId_Id").IsUnique().IsClustered();
    }
}
```

### Configuration Rules

1. **PK is NOT clustered** — Pass `false` to `EntityBaseConfiguration`. The clustered index goes on `(TenantId, Id)` for multi-tenant perf.
2. **Explicit table names** — Use `ToTable("{Entity}")` matching the class name (no pluralization)
3. **Restrict deletes** on parent references, **Cascade** on owned children
4. **Named indexes** — Use `HasDatabaseName("IX_{Entity}_{Column}")` convention
5. **MaxLength on all strings** — Always specify `HasMaxLength(N)` with a realistic length (e.g., Name→200, Email→254, Sku→50, Notes→2000). **Rarely** use `nvarchar(max)` — only for truly unbounded text like rich HTML or large JSON blobs.
6. **Decimal precision** — All `decimal` properties default to `decimal(10,4)` via `ConfigureDefaultDataTypes`. Override per-property only when needed (e.g., exchange rates→`decimal(18,8)`).
7. **DateTime → datetime2** — All `DateTime` and `DateTime?` properties map to SQL `datetime2`. The global convention in `ConfigureDefaultDataTypes` handles this. Never use the legacy `datetime` type.
8. **Default values for enums** — `HasDefaultValue({Enum}.None)` stores as numeric (default). Add `.HasConversion<string>()` only when human-readable DB values are needed (e.g., debugging, reporting); avoid string conversion on `[Flags]` enums used in bitwise queries.

## Repository Pattern

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Repositories/` — `TransactionalRepositories.cs` (write repos with includes), `QueryRepositories.cs` (read repos with projections and cacheable queries).

### Generic Repository

Provides base CRUD inherited from `RepositoryBase<TContext, TAuditId, TTenantId>`:

```csharp
public class GenericRepositoryTrxn({Project}DbContextTrxn dbContext) 
    : RepositoryBase<{Project}DbContextTrxn, string, Guid?>(dbContext), IGenericRepositoryTrxn { }
```

### Entity-Specific Repository

Create when entity needs custom queries or update logic:

```csharp
// Compact pattern — see sampleapp for full implementations
public class {Entity}RepositoryTrxn({Project}DbContextTrxn dbContext) 
    : RepositoryBase<{Project}DbContextTrxn, string, Guid?>(dbContext), I{Entity}RepositoryTrxn
{
    public async Task<{Entity}?> Get{Entity}Async(Guid id, bool includeChildren = false, CancellationToken ct = default)
    {
        var includes = includeChildren ? [q => q.Include(e => e.Children)] : [];
        return await GetEntityAsync(true, filter: e => e.Id == id, includes: includes, cancellationToken: ct);
    }
}
```

### Query Repository (Read-Only)

Separate interface + implementation using the Query DbContext:

```csharp
public class {Entity}RepositoryQuery({Project}DbContextQuery dbContext) 
    : RepositoryBase<{Project}DbContextQuery, string, Guid?>(dbContext), I{Entity}RepositoryQuery
{
    public async Task<PagedResponse<{Entity}Dto>> Search{Entity}Async(
        SearchRequest<{Entity}SearchFilter> request, CancellationToken ct = default)
    {
        return await QueryPageProjectionAsync({Entity}Mapper.ProjectorSearch, ...);
    }
}
```

## Repository Updater Pattern

Static extension methods on DbContext that handle child collection synchronization:

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Repositories/Updaters/TodoItemUpdater.cs`

```csharp
// Compact pattern — see sampleapp for full implementation
internal static class {Entity}Updater
{
    public static DomainResult<{Entity}> UpdateFromDto(
        this {Project}DbContextTrxn db, {Entity} entity, {Entity}Dto dto,
        RelatedDeleteBehavior relatedDeleteBehavior = RelatedDeleteBehavior.None)
    {
        return entity.Update(name: dto.Name)
            .Bind(updated => CollectionUtility.SyncCollectionWithResult(
                updated.Children, dto.Children,
                dbe => dbe.Id, dtoItem => dtoItem.Id,
                createDto => createDto.ToEntity().Bind(c => updated.AddChild(c)),
                (dbEntity, updateDto) => dbEntity.UpdateFromDto(updateDto),
                toRemove => { db.Delete(toRemove); return updated.RemoveChild(toRemove); }
            ).Map(updated));
    }
}
```

### Updater Rules

1. **Static extension on DbContext** — Needs DB access for delete operations
2. **Chain with Bind** — Entity.Update() → collection syncs → return
3. **CollectionUtility.SyncCollectionWithResult** — Handles create/update/remove in one pass
4. **RelatedDeleteBehavior** — Controls whether removes delete the entity or just the relationship
5. **Nested updaters** — Parent updater can delegate to child updaters (e.g., Team → TeamMember)

## Design-Time Factory

Required for EF migrations when running from CLI:

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Data/DesignTimeDbContextFactory.cs`

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

## Migrations

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Data/Migrations/` for InitialCreate migration (5 tables with non-clustered PK, clustered TenantId+Id, owned DateRange, self-ref FK, junction table).
>  
> Also see `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Data/Scripts/` for seed data, index maintenance, and user creation SQL.

## Migration Commands

```bash
# Set connection string
$env:EFCORETOOLSDB = "Server=localhost,1433;Database={Project}Db;Integrated Security=True;TrustServerCertificate=True"

# Add migration
dotnet ef migrations add {MigrationName} --context {Project}DbContextTrxn --project Infrastructure.Data

# Generate idempotent script
dotnet ef migrations script --idempotent --context {Project}DbContextTrxn --project Infrastructure.Data

# Apply migrations
dotnet ef database update --context {Project}DbContextTrxn --project Infrastructure.Data
```

## Database Registration (in Bootstrapper)

See [bootstrapper.md](bootstrapper.md) for full DI setup including:
- Pooled DbContext factory
- Scoped wrapper for per-request contexts
- Audit interceptor on Trxn context
- NoTracking + read replica on Query context
- Retry strategies

---

## Verification

After generating data access code, confirm:

- [ ] Two DbContexts: `{App}DbContextTrxn` (write) and `{App}DbContextQuery` (read, `UseQueryTrackingBehavior(NoTracking)`)
- [ ] Each entity has an `EntityTypeConfiguration` class implementing `IEntityTypeConfiguration<T>`
- [ ] `EntityBaseConfiguration<T>` is applied to all entities (Guid PK `ValueGeneratedNever`, `RowVersion` concurrency)
- [ ] Repository split: `I{Entity}RepositoryTrxn` for writes, `I{Entity}RepositoryQuery` for reads
- [ ] Query repository uses projector expressions (not `.ToDto()` method calls) for EF-safe queries
- [ ] Indexes defined in configuration match expected query patterns (unique constraints, composite indexes)
- [ ] Relationship delete behaviors are explicit: `Cascade` for owned children, `Restrict` for references
- [ ] Migration created after model changes with `dotnet ef migrations add {Name}`
- [ ] Cross-references: EF configs match [entity-template.md](../templates/entity-template.md) properties, repository interfaces in [repository-template.md](../templates/repository-template.md)
