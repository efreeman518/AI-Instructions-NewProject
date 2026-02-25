# Data Access

## Purpose

Use EF Core with split read/write contexts, explicit entity configurations, repository abstractions, and updater helpers for aggregate child synchronization.

## Non-Negotiables

1. Keep separate transactional and query DbContexts.
2. Keep shared schema/model conventions in a base DbContext.
3. Use explicit `IEntityTypeConfiguration<T>` per entity.
4. Separate write repositories from read/projection repositories.
5. Use updater pattern for child collection sync (create/update/remove in one pass).

Reference implementation: `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Data/` and `...Infrastructure.Repositories/`.

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

Base configuration:

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

Entity-specific configuration:

```csharp
public class {Entity}Configuration : EntityBaseConfiguration<{Entity}>
{
    public override void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        base.Configure(builder);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Id })
            .HasDatabaseName("CIX_{Entity}_TenantId_Id")
            .IsUnique()
            .IsClustered();
    }
}
```

### Configuration Rules

1. Keep PK non-clustered when clustered multi-tenant access index is used.
2. Use explicit table names (class-name aligned).
3. Set delete behavior explicitly (`Restrict` for references, `Cascade` for owned children where appropriate).
4. Name indexes predictably (`IX_...` / `CIX_...`).
5. Set `HasMaxLength(N)` for strings (avoid unnecessary `nvarchar(max)`).
6. Use default decimal precision and override only when domain requires it.
7. Use `datetime2` for `DateTime` columns.

---

## Repository Pattern

### Write Repository

```csharp
public class {Entity}RepositoryTrxn({Project}DbContextTrxn dbContext)
    : RepositoryBase<{Project}DbContextTrxn, string, Guid?>(dbContext), I{Entity}RepositoryTrxn
{
    public Task<{Entity}?> Get{Entity}Async(Guid id, bool includeChildren = false, CancellationToken ct = default)
    {
        var includes = includeChildren ? [q => q.Include(e => e.Children)] : [];
        return GetEntityAsync(true, filter: e => e.Id == id, includes: includes, cancellationToken: ct);
    }
}
```

### Query Repository

```csharp
public class {Entity}RepositoryQuery({Project}DbContextQuery dbContext)
    : RepositoryBase<{Project}DbContextQuery, string, Guid?>(dbContext), I{Entity}RepositoryQuery
{
    public Task<PagedResponse<{Entity}Dto>> Search{Entity}Async(
        SearchRequest<{Entity}SearchFilter> request,
        CancellationToken ct = default)
    {
        return QueryPageProjectionAsync({Entity}Mapper.ProjectorSearch, ...);
    }
}
```

Use EF-safe projector expressions for read models; avoid method calls that cannot translate server-side.

---

## Updater Pattern

Use static DbContext extension updaters to synchronize child collections with domain results:

```csharp
internal static class {Entity}Updater
{
    public static DomainResult<{Entity}> UpdateFromDto(
        this {Project}DbContextTrxn db,
        {Entity} entity,
        {Entity}Dto dto,
        RelatedDeleteBehavior relatedDeleteBehavior = RelatedDeleteBehavior.None)
    {
        return entity.Update(name: dto.Name)
            .Bind(updated => CollectionUtility.SyncCollectionWithResult(
                updated.Children,
                dto.Children,
                dbe => dbe.Id,
                dtoItem => dtoItem.Id,
                createDto => createDto.ToEntity().Bind(c => updated.AddChild(c)),
                (dbEntity, updateDto) => dbEntity.UpdateFromDto(updateDto),
                toRemove => { db.Delete(toRemove); return updated.RemoveChild(toRemove); })
            .Map(updated));
    }
}
```

Updater rules:

- keep update chains in `Bind` flow,
- centralize create/update/remove logic in one sync call,
- use `RelatedDeleteBehavior` to control relationship vs hard delete semantics.

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

```bash
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

## Verification

- [ ] both `{App}DbContextTrxn` and `{App}DbContextQuery` exist
- [ ] query context is configured for no-tracking reads
- [ ] each entity has explicit `IEntityTypeConfiguration<T>`
- [ ] repositories are split for write and read concerns
- [ ] read queries use projector expressions
- [ ] update paths use updater sync pattern for child collections
- [ ] migrations run through transactional context with design-time factory
- [ ] mappings/repositories align with [entity-template.md](../templates/entity-template.md) and [repository-template.md](../templates/repository-template.md)