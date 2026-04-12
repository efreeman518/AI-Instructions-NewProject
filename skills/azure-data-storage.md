# Azure Data Storage (Blob, Table, Cosmos DB)

Base types for each store come from dedicated `EF.*` packages — see [package-dependencies.md](package-dependencies.md) and the [EF.Packages repo](https://github.com/efreeman518/EF.Packages) for full API details.

## Prerequisites

- [package-dependencies.md](package-dependencies.md)
- [solution-structure.md](solution-structure.md)
- [bootstrapper.md](bootstrapper.md)
- [configuration-secrets.md](configuration-secrets.md)

## Overview

| Storage type | Best use | Partition strategy | Aspire resource |
|---|---|---|---|
| Blob Storage | Unstructured payloads (documents, media, exports, backups) | Container + blob path hierarchy | `AddAzureStorage().AddBlobs()` |
| Table Storage | Low-cost, high-volume key-value access; `PartitionKey + RowKey` driven queries | `PartitionKey` aligned to dominant query shape; `RowKey` unique within partition | `AddAzureStorage().AddTables()` |
| Cosmos DB | Document-first aggregates (nested JSON, high-throughput partitioned access, global distribution) | Dominant read/query path (e.g., `TenantId`) | `AddAzureCosmosDB()` |

Quick positioning against SQL:

- **SQL:** relational joins + transactions + FK relationships + migrations.
- **Cosmos DB:** schema-less documents + partition-aware access + single-document atomic writes. Tenant filtering is explicit (no EF global query filter).
- **Table Storage:** partitioned key-value entities, lowest-cost transaction model.

---

## Common Patterns

All three stores follow the same structural conventions.

### Settings Class

Each store has a project-specific settings POCO that inherits the package base:

```csharp
public class {Project}{Store}RepositorySettings : {Store}RepositorySettingsBase { }
```

| Store | Base class | Key setting |
|---|---|---|
| Blob | `BlobRepositorySettingsBase` | `BlobServiceClientName` |
| Table | `TableRepositorySettingsBase` | `TableServiceClientName` |
| Cosmos DB | `CosmosDbRepositorySettingsBase` | `CosmosDbId` |

### Repository Wrapper

Each store exposes a project-specific interface + implementation deriving from the package base:

```csharp
public interface I{Project}{Store}Repository : I{Store}Repository { }

public class {Project}{Store}Repository : {Store}RepositoryBase, I{Project}{Store}Repository
{
    public {Project}{Store}Repository(
        ILogger<{Project}{Store}Repository> logger,
        IOptions<{Project}{Store}RepositorySettings> settings,
        ...)                           // Blob/Table: + IAzureClientFactory<*ServiceClient>
        : base(logger, settings, ...) { }
}
```

### DI Registration (Bootstrapper)

Registration follows the same three-step pattern in each `Add{Store}Services` method:

1. Register a **named service client** via `AddAzureClients`.
2. **Bind settings** from configuration.
3. Register the **scoped repository**.

```csharp
private static void Add{Store}Services(IServiceCollection services, IConfiguration config)
{
    services.AddAzureClients(builder =>
    {
        builder.Add{Store}ServiceClient(config.GetConnectionString("{ConnectionName}")!)
            .WithName("{Project}{Store}Client");
    });

    services.Configure<{Project}{Store}RepositorySettings>(
        config.GetSection("{Project}{Store}RepositorySettings"));

    services.AddScoped<I{Project}{Store}Repository, {Project}{Store}Repository>();
}
```

### Aspire Integration

Blob and Table share an `AzureStorage` resource with emulator support. Cosmos DB uses its own resource.

> **Version constraint:** When using `IAzureClientFactory<T>` alongside `EF.Host`, `Microsoft.Extensions.Azure` must be ≥1.12.0 (pulled transitively by `EF.Host` → `AzureAppConfiguration`). Lower versions cause assembly version conflicts.

> **Cosmos DB dependency:** `Microsoft.Azure.Cosmos` requires `Newtonsoft.Json ≥10.0.2`. Add an explicit `Newtonsoft.Json` entry to `Directory.Packages.props` when adding Cosmos.

```csharp
// Blob + Table (shared Azure Storage resource)
var storage = builder.AddAzureStorage("AzureStorage").RunAsEmulator();
var blobs  = storage.AddBlobs("BlobStorage1");
var tables = storage.AddTables("TableStorage1");

// Cosmos DB
var cosmos = builder.AddAzureCosmosDB("CosmosDb1").RunAsEmulator();

builder.AddProject<Projects.{Project}_Api>("{project}-api")
    .WithReference(blobs)
    .WithReference(tables)
    .WithReference(cosmos);
```

---

## Blob Storage

### Purpose

Use Blob Storage for unstructured payloads (documents, media, exports, backups). Keep relational/queryable data in SQL/Cosmos/Table as appropriate.

### Non-Negotiables

1. Access blobs through `IBlobRepository` abstraction.
2. Register storage with named `BlobServiceClient` via `IAzureClientFactory`.
3. Use scoped SAS permissions and short expiry windows.
4. Keep container access private unless explicitly required.
5. Dispose downloaded streams correctly.

### Repository Contract

```csharp
public interface IBlobRepository
{
    Task CreateContainerAsync(ContainerInfo containerInfo, CancellationToken cancellationToken = default);
    Task DeleteContainerAsync(ContainerInfo containerInfo, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<BlobItem>, string?)> QueryPageBlobsAsync(
        ContainerInfo containerInfo,
        string? continuationToken = null,
        BlobTraits blobTraits = BlobTraits.None,
        BlobStates blobStates = BlobStates.None,
        string? prefix = null,
        CancellationToken cancellationToken = default);

    Task<IAsyncEnumerable<BlobItem>> GetStreamBlobList(
        ContainerInfo containerInfo,
        BlobTraits blobTraits = BlobTraits.None,
        BlobStates blobStates = BlobStates.None,
        string? prefix = null,
        CancellationToken cancellationToken = default);

    Task<Uri?> GenerateBlobSasUriAsync(
        ContainerInfo containerInfo,
        string blobName,
        BlobSasPermissions permissions,
        DateTimeOffset expiresOn,
        SasIPRange? ipRange = null,
        CancellationToken cancellationToken = default);

    Task UploadBlobStreamAsync(
        ContainerInfo containerInfo,
        string blobName,
        Stream stream,
        string? contentType = null,
        bool encrypt = false,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task UploadBlobStreamAsync(
        Uri sasUri,
        Stream stream,
        string? contentType = null,
        bool encrypt = false,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<Stream> StartDownloadBlobStreamAsync(
        ContainerInfo containerInfo,
        string blobName,
        bool decrypt = false,
        CancellationToken cancellationToken = default);

    Task<Stream> StartDownloadBlobStreamAsync(
        Uri sasUri,
        bool decrypt = false,
        CancellationToken cancellationToken = default);

    Task DeleteBlobAsync(ContainerInfo containerInfo, string blobName, CancellationToken cancellationToken = default);
    Task DeleteBlobAsync(Uri sasUri, CancellationToken cancellationToken = default);
}
```

Supporting types:

```csharp
public class ContainerInfo
{
    public string ContainerName { get; set; } = null!;
    public ContainerPublicAccessType ContainerPublicAccessType { get; set; } = ContainerPublicAccessType.None;
    public bool CreateContainerIfNotExist { get; set; } = true;
}

public enum ContainerPublicAccessType
{
    None = 0,
    BlobContainer = 1,
    Blob = 2
}
```

### Project Repository Wrapper

```csharp
public interface I{Project}BlobRepository : IBlobRepository { }

public class {Project}BlobRepository : BlobRepositoryBase, I{Project}BlobRepository
{
    public {Project}BlobRepository(
        ILogger<{Project}BlobRepository> logger,
        IOptions<{Project}BlobRepositorySettings> settings,
        IAzureClientFactory<BlobServiceClient> clientFactory)
        : base(logger, settings, clientFactory) { }
}

public class {Project}BlobRepositorySettings : BlobRepositorySettingsBase { }
```

`BlobRepositorySettingsBase` requires `BlobServiceClientName`.

### Configuration

`appsettings.json`

```json
{
  "ConnectionStrings": {
    "BlobStorage1": ""
  },
  "{Project}BlobRepositorySettings": {
    "BlobServiceClientName": "{Project}BlobClient"
  }
}
```

`appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "BlobStorage1": "UseDevelopmentStorage=true"
  }
}
```

### DI Registration

```csharp
private static void AddBlobStorageServices(IServiceCollection services, IConfiguration config)
{
    services.AddAzureClients(builder =>
    {
        builder.AddBlobServiceClient(config.GetConnectionString("BlobStorage1")!)
            .WithName("{Project}BlobClient");
    });

    services.Configure<{Project}BlobRepositorySettings>(
        config.GetSection("{Project}BlobRepositorySettings"));

    services.AddScoped<I{Project}BlobRepository, {Project}BlobRepository>();
}
```

### Usage Patterns

- **Server upload/download/delete:** repository with `ContainerInfo`.
- **Client direct upload/download:** generate temporary SAS URI with minimal permissions.
- **Large listings:** continuation-token paging or stream enumeration.
- **Cross-instance lock:** blob lease/distributed lock execution where needed.

Blob naming patterns:

- `{tenantId}/{entityType}/{entityId}/{filename}`
- `{guid}/{filename}`
- `{yyyy}/{MM}/{dd}/{filename}`

---

## Table Storage

### Purpose

Use Table Storage for low-cost, high-volume key-value access where queries are primarily `PartitionKey + RowKey` driven.

### Non-Negotiables

1. Entities must implement `Azure.Data.Tables.ITableEntity`.
2. `PartitionKey` design follows dominant query shape.
3. `RowKey` is unique within partition and supports required ordering.
4. Access data through repository abstractions (`ITableRepository` wrappers).
5. Use named `TableServiceClient` registration via `IAzureClientFactory`.
6. For timeline/audit streams, define retention, archival, and replay expectations explicitly.

### Entity Pattern

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

### Repository Contract

`ITableRepository` supports:

- point operations (`GetItemAsync`, `CreateItemAsync`, `UpsertItemAsync`, `UpdateItemAsync`, `DeleteItemAsync`),
- pagination (`QueryPageAsync` with LINQ/OData filters),
- streaming (`GetStream<T>()`),
- table lifecycle (`GetOrCreateTableAsync`, `DeleteTableAsync`).

### Project Repository Wrapper

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

### Configuration

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

### DI Registration

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

### Usage Patterns

- **Audit/event log:** append records with inverse-tick `RowKey`.
- **Lookup/config store:** `PartitionKey=category`, `RowKey=setting-key`.
- **Large scans:** prefer continuation-token pagination or streaming APIs.

If table entities participate in mixed-store workflows, include reconciliation checks against the authoritative source.

Avoid relationship-heavy data models; denormalize where necessary.

---

## Cosmos DB

### Purpose

Use Cosmos DB for document-first aggregates (nested JSON, high-throughput partitioned access, globally distributed reads/writes). Keep relational workflows in SQL/EF Core when data requires joins, foreign keys, and cross-aggregate transaction boundaries.

### Non-Negotiables

1. Every Cosmos entity inherits `CosmosDbEntity` and overrides `PartitionKey`.
2. Partition key is chosen from dominant query patterns (not convenience).
3. Data access goes through `ICosmosDbRepository` abstraction.
4. DI wiring is centralized in Bootstrapper.
5. No EF entity configuration classes and no migrations for Cosmos entities.
6. For high-ingest entities, define throughput profile, TTL/retention, and replay window before implementation.

### Canonical Entity Pattern

```csharp
namespace {Project}.Domain.Model;

public class TodoItemDocument : CosmosDbEntity
{
    public override string PartitionKey => TenantId.ToString();

    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public Schedule Schedule { get; private set; } = new();
    public List<ChecklistItem> ChecklistItems { get; private set; } = [];
    public Dictionary<string, string> Metadata { get; private set; } = [];

    public static DomainResult<TodoItemDocument> Create(Guid tenantId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return DomainResult<TodoItemDocument>.Failure("Title is required.");

        return DomainResult<TodoItemDocument>.Success(new TodoItemDocument
        {
            TenantId = tenantId,
            Title = title
        });
    }
}

public class Schedule
{
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
}

public class ChecklistItem
{
    public string Description { get; set; } = null!;
    public bool IsComplete { get; set; }
    public int SortOrder { get; set; }
}
```

### Embedded Model Guidance

- Use nested objects/arrays when children are always loaded and saved with the parent.
- Use dictionary metadata for sparse/variable attributes.
- If child records need independent querying/lifecycle, model them as separate documents or move to SQL.

### Partition Key Guidance

- Tenant-based (`TenantId.ToString()`) for tenant-scoped access.
- Category/group keys for high-volume grouped queries.
- Composite keys (`$"{TenantId}:{Region}"`) only when both dimensions are common filters.
- Avoid random keys that force cross-partition queries for normal reads.

### Operational Controls

- Throughput: document expected RU profile (`standard|high|burst`) and autoscale strategy.
- Lifecycle: define TTL/retention/archival expectations per container or entity family.
- Replay: define backfill/replay window and consistency expectations for out-of-order events.

### Repository Contract

```csharp
public interface ICosmosDbRepository
{
    Task<T> SaveItemAsync<T>(T item) where T : CosmosDbEntity;
    Task<T?> GetItemAsync<T>(string id, string partitionKey) where T : CosmosDbEntity;
    Task DeleteItemAsync<T>(string id, string partitionKey);
    Task DeleteItemAsync<T>(T item) where T : CosmosDbEntity;

    Task<(List<TProject>, int, string?)> QueryPageProjectionAsync<TSource, TProject>(
        string? continuationToken = null,
        int pageSize = 10,
        Expression<Func<TProject, bool>>? filter = null,
        List<Sort>? sorts = null,
        bool includeTotal = false,
        int maxConcurrency = -1,
        CancellationToken cancellationToken = default);

    Task<(List<TProject>, int, string?)> QueryPageProjectionAsync<TSource, TProject>(
        string? continuationToken = null,
        int pageSize = 10,
        string? sql = null,
        string? sqlCount = null,
        Dictionary<string, object>? parameters = null,
        int maxConcurrency = -1,
        CancellationToken cancellationToken = default);

    Task<Container> GetOrAddContainerAsync(string containerId, string? partitionKeyPath = null);
    Task<HttpStatusCode?> DeleteContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task<HttpStatusCode> SetOrCreateDatabaseAsync(string dbId, int? throughput = null, CancellationToken cancellationToken = default);
    Task<HttpStatusCode> DeleteDatabaseAsync(string? dbId = null, CancellationToken cancellationToken = default);
}
```

### Project Repository Wrapper

```csharp
namespace {Project}.Infrastructure.Repositories;

public interface I{Project}CosmosDbRepository : ICosmosDbRepository { }

public class {Project}CosmosDbRepository : CosmosDbRepositoryBase, I{Project}CosmosDbRepository
{
    public {Project}CosmosDbRepository(
        ILogger<{Project}CosmosDbRepository> logger,
        IOptions<{Project}CosmosDbRepositorySettings> settings)
        : base(logger, settings)
    {
    }
}

public class {Project}CosmosDbRepositorySettings : CosmosDbRepositorySettingsBase { }
```

### Configuration

`appsettings.json`

```json
{
  "ConnectionStrings": {
    "CosmosDb1": ""
  },
  "{Project}CosmosDbRepositorySettings": {
    "CosmosDbId": "{project}-db"
  }
}
```

`appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "CosmosDb1": "AccountEndpoint=https://localhost:8081/;AccountKey=<emulator-key>"
  }
}
```

For deployed environments, prefer managed identity / Key Vault.

### DI Registration

```csharp
private static void AddCosmosDbServices(IServiceCollection services, IConfiguration config)
{
    services.AddAzureClients(builder =>
    {
        builder.AddCosmosServiceClient(config.GetConnectionString("CosmosDb1")!)
            .WithName("{Project}CosmosClient");
    });

    services.Configure<{Project}CosmosDbRepositorySettings>(options =>
    {
        config.GetSection("{Project}CosmosDbRepositorySettings").Bind(options);
    });

    services.AddScoped<I{Project}CosmosDbRepository, {Project}CosmosDbRepository>();
}
```

Direct `CosmosClient` injection in settings is acceptable when `IAzureClientFactory` is not used.

### Service Usage Pattern

```csharp
public class TodoItemService(I{Project}CosmosDbRepository cosmosRepo, IRequestContext<string, Guid?> requestContext)
{
    public async Task<Result<TodoItemDocument>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var tenantPk = requestContext.TenantId!.Value.ToString();
        var item = await cosmosRepo.GetItemAsync<TodoItemDocument>(id.ToString(), tenantPk);
        return item is null ? Result<TodoItemDocument>.None() : Result<TodoItemDocument>.Success(item);
    }
}
```

### Querying

- LINQ: `QueryPageProjectionAsync<TSource, TProject>(filter, sorts, includeTotal: true)` for type-safe pagination.
- SQL: overload with `sql`, `sqlCount`, and parameters for advanced queries.
- Large scans: use streaming APIs where available to avoid loading full datasets.

---

## Verification

### Blob Storage

- [ ] Repository derives from `BlobRepositoryBase`
- [ ] Settings derive from `BlobRepositorySettingsBase`
- [ ] Named `BlobServiceClient` registration exists
- [ ] Container names/access levels are explicit
- [ ] SAS generation uses least privilege + short expiry
- [ ] Download stream lifecycle is correctly disposed
- [ ] Local dev uses `UseDevelopmentStorage=true` (Azurite)
- [ ] Storage connection naming aligns with Aspire/IaC

### Table Storage

- [ ] Entity implements `ITableEntity` (`PartitionKey`, `RowKey`, `Timestamp`, `ETag`)
- [ ] `PartitionKey` and `RowKey` match access/query patterns
- [ ] Repository derives from `TableRepositoryBase`
- [ ] Settings derive from `TableRepositorySettingsBase`
- [ ] Named `TableServiceClient` registration is present
- [ ] Local dev uses `UseDevelopmentStorage=true` (Azurite)
- [ ] Pagination/streaming path is used for non-trivial scans
- [ ] Resource naming matches Aspire/IaC storage configuration

### Cosmos DB

- [ ] Entity inherits `CosmosDbEntity` and overrides `PartitionKey`
- [ ] Partition key aligns with dominant read/query path
- [ ] Repository derives from `CosmosDbRepositoryBase` and exposes `I{Project}CosmosDbRepository`
- [ ] Settings derive from `CosmosDbRepositorySettingsBase` and include `CosmosDbId`
- [ ] Bootstrapper registers Cosmos client + repository
- [ ] Service calls pass both `id` and `partitionKey`
- [ ] No EF migrations/config classes for Cosmos entities
- [ ] Cross-reference alignment with [domain-model.md](domain-model.md) and [application-layer.md](application-layer.md)
