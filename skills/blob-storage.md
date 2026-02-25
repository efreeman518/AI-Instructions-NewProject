# Blob Storage

## Prerequisites

- [package-dependencies.md](package-dependencies.md)
- [solution-structure.md](solution-structure.md)
- [bootstrapper.md](bootstrapper.md)
- [configuration.md](configuration.md)

## Purpose

Use Blob Storage for unstructured payloads (documents, media, exports, backups). Keep relational/queryable data in SQL/Cosmos/Table as appropriate.

## Non-Negotiables

1. Access blobs through `IBlobRepository` abstraction.
2. Register storage with named `BlobServiceClient` via `IAzureClientFactory`.
3. Use scoped SAS permissions and short expiry windows.
4. Keep container access private unless explicitly required.
5. Dispose downloaded streams correctly.

---

## Repository Contract

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

---

## Project Repository Wrapper

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

---

## Configuration

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

---

## DI Registration (Bootstrapper)

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

---

## Usage Patterns

- **Server upload/download/delete:** repository with `ContainerInfo`.
- **Client direct upload/download:** generate temporary SAS URI with minimal permissions.
- **Large listings:** continuation-token paging or stream enumeration.
- **Cross-instance lock:** blob lease/distributed lock execution where needed.

Blob naming patterns:

- `{tenantId}/{entityType}/{entityId}/{filename}`
- `{guid}/{filename}`
- `{yyyy}/{MM}/{dd}/{filename}`

---

## Aspire Integration

```csharp
var storage = builder.AddAzureStorage("AzureStorage").RunAsEmulator();
var blobs = storage.AddBlobs("BlobStorage1");
builder.AddProject<Projects.{Project}_Api>("{project}-api").WithReference(blobs);
```

---

## Verification

- [ ] repository derives from `BlobRepositoryBase`
- [ ] settings derive from `BlobRepositorySettingsBase`
- [ ] named `BlobServiceClient` registration exists
- [ ] container names/access levels are explicit
- [ ] SAS generation uses least privilege + short expiry
- [ ] download stream lifecycle is correctly disposed
- [ ] local dev uses `UseDevelopmentStorage=true` (Azurite)
- [ ] storage connection naming aligns with Aspire/IaC