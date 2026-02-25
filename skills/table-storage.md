# Table Storage

## Prerequisites

- [package-dependencies.md](package-dependencies.md)
- [solution-structure.md](solution-structure.md)
- [bootstrapper.md](bootstrapper.md)
- [configuration.md](configuration.md)

## Purpose

Use Table Storage for low-cost, high-volume key-value access where queries are primarily `PartitionKey + RowKey` driven.

## Non-Negotiables

1. Entities must implement `Azure.Data.Tables.ITableEntity`.
2. `PartitionKey` design follows dominant query shape.
3. `RowKey` is unique within partition and supports required ordering.
4. Access data through repository abstractions (`ITableRepository` wrappers).
5. Use named `TableServiceClient` registration via `IAzureClientFactory`.

---

## Entity Pattern

```csharp
public class {Entity}TableEntity : Azure.Data.Tables.ITableEntity
{
    public string PartitionKey { get; set; } = null!;
    public string RowKey { get; set; } = null!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = null!;
    public string Status { get; set; } = "Active";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
```

### Key Strategy Guidance

| Strategy | PartitionKey | RowKey | Typical Use |
|---|---|---|---|
| Tenant + Id | `TenantId` | `EntityId` | tenant-scoped lookups |
| Type + inverse time | `"AuditLog"` | `{InverseTicks}_{Guid}` | recent-first event streams |
| Category + key | `Category` | `ItemKey` | lookup/config tables |
| Composite | `{TenantId}:{Year}` | `{EntityId}` | tenant + time partitioning |

Use inverse ticks for newest-first ordering:

```csharp
var rowKey = $"{DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks:D19}_{Guid.NewGuid()}";
```

---

## Repository Contract

`ITableRepository` supports:

- point operations (`GetItemAsync`, `CreateItemAsync`, `UpsertItemAsync`, `UpdateItemAsync`, `DeleteItemAsync`),
- pagination (`QueryPageAsync` with LINQ/OData filters),
- streaming (`GetStream<T>()`),
- table lifecycle (`GetOrCreateTableAsync`, `DeleteTableAsync`).

### Project Wrapper Pattern

```csharp
public interface I{Project}TableRepository : ITableRepository { }

public class {Project}TableRepository : TableRepositoryBase, I{Project}TableRepository
{
    public {Project}TableRepository(
        ILogger<{Project}TableRepository> logger,
        IOptions<{Project}TableRepositorySettings> settings,
        IAzureClientFactory<TableServiceClient> clientFactory)
        : base(logger, settings, clientFactory) { }
}

public class {Project}TableRepositorySettings : TableRepositorySettingsBase { }
```

`TableRepositorySettingsBase` requires `TableServiceClientName`.

`TableUpdateMode` usage:

- `Merge` for partial updates,
- `Replace` for full entity overwrite.

---

## Configuration

`appsettings.json`

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

`appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "TableStorage1": "UseDevelopmentStorage=true"
  }
}
```

---

## DI Registration (Bootstrapper)

```csharp
private static void AddTableStorageServices(IServiceCollection services, IConfiguration config)
{
    services.AddAzureClients(builder =>
    {
        builder.AddTableServiceClient(config.GetConnectionString("TableStorage1")!)
            .WithName("{Project}TableClient");
    });

    services.Configure<{Project}TableRepositorySettings>(
        config.GetSection("{Project}TableRepositorySettings"));

    services.AddScoped<I{Project}TableRepository, {Project}TableRepository>();
}
```

---

## Usage Patterns

- **Audit/event log:** append records with inverse-tick `RowKey`.
- **Lookup/config store:** `PartitionKey=category`, `RowKey=setting-key`.
- **Large scans:** prefer continuation-token pagination or streaming APIs.

Avoid relationship-heavy data models; denormalize where necessary.

---

## Aspire Integration

```csharp
var storage = builder.AddAzureStorage("AzureStorage").RunAsEmulator();
var tables = storage.AddTables("TableStorage1");
builder.AddProject<Projects.{Project}_Api>("{project}-api").WithReference(tables);
```

---

## Quick Comparison

- SQL: relational joins + transactions.
- Cosmos DB: document/nested aggregate model.
- Table Storage: partitioned key-value entities, lowest-cost transaction model.

---

## Verification

- [ ] entity implements `ITableEntity` (`PartitionKey`, `RowKey`, `Timestamp`, `ETag`)
- [ ] `PartitionKey` and `RowKey` match access/query patterns
- [ ] repository derives from `TableRepositoryBase`
- [ ] settings derive from `TableRepositorySettingsBase`
- [ ] named `TableServiceClient` registration is present
- [ ] local dev uses `UseDevelopmentStorage=true` (Azurite)
- [ ] pagination/streaming path is used for non-trivial scans
- [ ] resource naming matches Aspire/IaC storage configuration