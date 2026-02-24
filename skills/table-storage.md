# Table Storage

## Prerequisites

- [package-dependencies.md](package-dependencies.md) — `EF.Table` package types
- [solution-structure.md](solution-structure.md) — project layout and Infrastructure layer conventions
- [bootstrapper.md](bootstrapper.md) — centralized DI registration
- [configuration.md](configuration.md) — appsettings and secrets management

## Overview

Table Storage access uses `EF.Table` which provides a **repository abstraction** over Azure Table Storage via the Azure SDK `TableServiceClient`. Use Table Storage for structured NoSQL key-value data — audit logs, lookup tables, configuration data, lightweight event stores, and denormalized read models.

> **When to use Table Storage vs SQL vs Cosmos DB:** Use Table Storage for high-volume, low-cost, key-value data with simple query patterns (filter by PartitionKey + RowKey). Use SQL for relational data. Use Cosmos DB for complex document queries, global distribution, or rich indexing.

---

## Entity Pattern

Table entities implement `Azure.Data.Tables.ITableEntity` (the package provides a convenience `ITableEntity` wrapper so consumers don't need a direct Azure SDK reference):

```csharp
namespace {Project}.Domain.Model;

public class {Entity}TableEntity : Azure.Data.Tables.ITableEntity
{
    // Required by ITableEntity
    public string PartitionKey { get; set; } = null!;
    public string RowKey { get; set; } = null!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Domain properties
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
```

### Key Design

| Strategy | PartitionKey | RowKey | Query Pattern |
|----------|-------------|--------|---------------|
| Tenant + Entity | `TenantId` | `EntityId` | Single tenant lookups |
| Entity Type + Date | `"AuditLog"` | `{InverseTicks}_{Guid}` | Recent-first time queries |
| Category + Id | `Category` | `ItemId` | Category scoped lookups |
| Composite | `{TenantId}:{Year}` | `{TodoItemId}` | Tenant + time-range queries |

> **Key rules:**
> - **PartitionKey** determines physical partitioning and query performance — queries within a single partition are fastest
> - **RowKey** must be unique within a partition
> - For time-ordered queries, use **inverse ticks** (`string.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks)`) so newest items sort first
> - The repository uses `typeof(T).Name` as the table name automatically

---

## Repository Interface

The package provides `ITableRepository`:

```csharp
public interface ITableRepository
{
    Task<T?> GetItemAsync<T>(string partitionKey, string rowkey,
        IEnumerable<string>? selectProps = null, CancellationToken cancellationToken = default)
        where T : class, Azure.Data.Tables.ITableEntity;

    Task<HttpStatusCode> CreateItemAsync<T>(T item, CancellationToken cancellationToken = default)
        where T : Azure.Data.Tables.ITableEntity;

    Task<HttpStatusCode> UpsertItemAsync<T>(T item, TableUpdateMode updateMode,
        CancellationToken cancellationToken = default)
        where T : Azure.Data.Tables.ITableEntity;

    Task<HttpStatusCode> UpdateItemAsync<T>(T item, TableUpdateMode updateMode,
        CancellationToken cancellationToken = default)
        where T : Azure.Data.Tables.ITableEntity;

    Task<HttpStatusCode> DeleteItemAsync<T>(string partitionKey, string rowkey,
        CancellationToken cancellationToken = default);

    // Paged queries — LINQ or OData filter
    Task<(IReadOnlyList<T>?, string?)> QueryPageAsync<T>(
        string? continuationToken = null, int pageSize = 10,
        Expression<Func<T, bool>>? filterLinq = null, string? filterOData = null,
        IEnumerable<string>? selectProps = null, CancellationToken cancellationToken = default)
        where T : class, Azure.Data.Tables.ITableEntity;

    // Streaming queries
    IAsyncEnumerable<T> GetStream<T>(
        Expression<Func<T, bool>>? filterLinq = null, string? filterOData = null,
        IEnumerable<string>? selectProps = null, CancellationToken cancellationToken = default)
        where T : class, Azure.Data.Tables.ITableEntity;

    // Table management
    Task<TableClient> GetOrCreateTableAsync(string tableName, CancellationToken cancellationToken = default);
    Task<HttpStatusCode> DeleteTableAsync(string tableName, CancellationToken cancellationToken = default);
}
```

### TableUpdateMode

```csharp
public enum TableUpdateMode
{
    Merge = 0,    // Merge properties (partial update)
    Replace = 1   // Replace entire entity
}
```

---

## Concrete Repository

```csharp
namespace {Project}.Infrastructure.Repositories;

public class {Project}TableRepository : TableRepositoryBase, I{Project}TableRepository
{
    public {Project}TableRepository(
        ILogger<{Project}TableRepository> logger,
        IOptions<{Project}TableRepositorySettings> settings,
        IAzureClientFactory<TableServiceClient> clientFactory)
        : base(logger, settings, clientFactory)
    {
    }
}

public interface I{Project}TableRepository : ITableRepository { }
```

### Settings

```csharp
namespace {Project}.Infrastructure.Repositories;

public class {Project}TableRepositorySettings : TableRepositorySettingsBase { }
```

`TableRepositorySettingsBase` provides:

```csharp
public abstract class TableRepositorySettingsBase
{
    public string TableServiceClientName { get; set; } = null!;
}
```

---

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "TableStorage1": ""
  },
  "{Project}TableRepositorySettings": {
    "TableServiceClientName": "{Project}TableClient"
  }
}
```

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "TableStorage1": "UseDevelopmentStorage=true"
  }
}
```

---

## DI Registration (in Bootstrapper)

```csharp
private static void AddTableStorageServices(IServiceCollection services, IConfiguration config)
{
    // Register named TableServiceClient via IAzureClientFactory
    services.AddAzureClients(builder =>
    {
        builder.AddTableServiceClient(config.GetConnectionString("TableStorage1")!)
            .WithName("{Project}TableClient");
    });

    // Bind settings
    services.Configure<{Project}TableRepositorySettings>(
        config.GetSection("{Project}TableRepositorySettings"));

    // Register repository
    services.AddScoped<I{Project}TableRepository, {Project}TableRepository>();
}
```

---

## Service Layer Usage

### Audit Log Example

```csharp
public class AuditLogService(
    I{Project}TableRepository tableRepo,
    IRequestContext<string, Guid?> requestContext,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task<Result> LogAsync(string action, string entityType, Guid entityId,
        CancellationToken ct = default)
    {
        var entry = new AuditLogTableEntity
        {
            PartitionKey = requestContext.TenantId.ToString()!,
            RowKey = $"{DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks:D19}_{Guid.NewGuid()}",
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            UserId = requestContext.AuditId ?? "system",
            CreatedUtc = DateTime.UtcNow
        };

        var status = await tableRepo.CreateItemAsync(entry, ct);
        return status == HttpStatusCode.NoContent
            ? Result.Success()
            : Result.Failure($"Table insert returned {status}");
    }

    public async Task<Result<PagedResponse<AuditLogTableEntity>>> SearchAsync(
        string? continuationToken, int pageSize, CancellationToken ct = default)
    {
        var tenantId = requestContext.TenantId.ToString()!;
        var (items, nextToken) = await tableRepo.QueryPageAsync<AuditLogTableEntity>(
            continuationToken, pageSize,
            filterLinq: e => e.PartitionKey == tenantId,
            cancellationToken: ct);

        return Result<PagedResponse<AuditLogTableEntity>>.Success(
            new PagedResponse<AuditLogTableEntity>
            {
                Data = items?.ToList() ?? [],
                PageSize = pageSize
            });
    }
}
```

### Lookup Table Example

```csharp
public async Task<Result<ConfigSettingTableEntity?>> GetSettingAsync(
    string category, string key, CancellationToken ct = default)
{
    var item = await tableRepo.GetItemAsync<ConfigSettingTableEntity>(category, key, cancellationToken: ct);
    return item is not null
        ? Result<ConfigSettingTableEntity?>.Success(item)
        : Result<ConfigSettingTableEntity?>.None();
}

public async Task<Result> UpsertSettingAsync(
    string category, string key, string value, CancellationToken ct = default)
{
    var entity = new ConfigSettingTableEntity
    {
        PartitionKey = category,
        RowKey = key,
        Value = value,
        UpdatedUtc = DateTime.UtcNow
    };

    await tableRepo.UpsertItemAsync(entity, TableUpdateMode.Replace, ct);
    return Result.Success();
}
```

---

## Aspire Integration

In `AppHost/Program.cs`:

```csharp
var storage = builder.AddAzureStorage("AzureStorage")
    .RunAsEmulator();  // Uses Azurite locally

var tables = storage.AddTables("TableStorage1");

var api = builder.AddProject<Projects.{Project}_Api>("{project}-api")
    .WithReference(tables);
```

---

## Differences from EF Core/SQL and Cosmos DB

| Concern | EF Core/SQL | Cosmos DB | Table Storage |
|---------|-------------|-----------|---------------|
| Entity base | `EntityBase` | `CosmosDbEntity` | `ITableEntity` |
| Key model | `Guid Id` | `id` + `PartitionKey` | `PartitionKey` + `RowKey` (both strings) |
| Schema | Strongly typed via EF config | Schema-less JSON | Flat key-value (no nesting) |
| Querying | Full LINQ + SQL | LINQ + SQL + continuation | LINQ/OData + continuation |
| Relationships | Navigation properties, FK | Embedded or manual | None (denormalize) |
| Cost model | DTU/vCore based | RU-based | Per-transaction (very low) |
| Best for | Relational, transactional | Document, partition-heavy | Key-value, audit, config |

---

## Verification

After generating Table Storage code, confirm:

- [ ] Entity implements `Azure.Data.Tables.ITableEntity` with `PartitionKey`, `RowKey`, `Timestamp`, `ETag`
- [ ] Repository inherits `TableRepositoryBase` with project-specific settings
- [ ] Settings class inherits `TableRepositorySettingsBase` with `TableServiceClientName`
- [ ] DI registers named `TableServiceClient` via `IAzureClientFactory<TableServiceClient>`
- [ ] PartitionKey design matches primary query pattern
- [ ] RowKey ensures uniqueness within partition (consider inverse ticks for time-ordered data)
- [ ] Connection string uses `UseDevelopmentStorage=true` for local Azurite
- [ ] Cross-references: Aspire resource matches connection string name; IaC provisions storage account
