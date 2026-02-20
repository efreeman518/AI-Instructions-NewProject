# Blob Storage

## Prerequisites

- [package-dependencies.md](package-dependencies.md) — `Package.Infrastructure.Storage` package types
- [solution-structure.md](solution-structure.md) — project layout and Infrastructure layer conventions
- [bootstrapper.md](bootstrapper.md) — centralized DI registration
- [configuration.md](configuration.md) — appsettings and secrets management

## Overview

Blob Storage access uses `Package.Infrastructure.Storage` which provides a **repository abstraction** over Azure Blob Storage via the Azure SDK `BlobServiceClient`. Use Blob Storage for unstructured binary data — file uploads, document attachments, images, backups, and large payloads.

> **When to use Blob Storage:** Use for files, images, documents, backups, or any binary/large-text content. Not for structured queryable data (use SQL or Cosmos DB) or key-value lookups (use Table Storage).

---

## Repository Interface

The package provides `IBlobRepository`:

```csharp
public interface IBlobRepository
{
    // Container management
    Task CreateContainerAsync(ContainerInfo containerInfo, CancellationToken cancellationToken = default);
    Task DeleteContainerAsync(ContainerInfo containerInfo, CancellationToken cancellationToken = default);

    // List / query blobs
    Task<(IReadOnlyList<BlobItem>, string?)> QueryPageBlobsAsync(
        ContainerInfo containerInfo, string? continuationToken = null,
        BlobTraits blobTraits = BlobTraits.None, BlobStates blobStates = BlobStates.None,
        string? prefix = null, CancellationToken cancellationToken = default);
    Task<IAsyncEnumerable<BlobItem>> GetStreamBlobList(ContainerInfo containerInfo,
        BlobTraits blobTraits = BlobTraits.None, BlobStates blobStates = BlobStates.None,
        string? prefix = null, CancellationToken cancellationToken = default);

    // SAS URI generation
    Task<Uri?> GenerateBlobSasUriAsync(ContainerInfo containerInfo, string blobName,
        BlobSasPermissions permissions, DateTimeOffset expiresOn,
        SasIPRange? ipRange = null, CancellationToken cancellationToken = default);

    // Upload
    Task UploadBlobStreamAsync(ContainerInfo containerInfo, string blobName, Stream stream,
        string? contentType = null, bool encrypt = false,
        IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
    Task UploadBlobStreamAsync(Uri sasUri, Stream stream,
        string? contentType = null, bool encrypt = false,
        IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    // Download
    Task<Stream> StartDownloadBlobStreamAsync(ContainerInfo containerInfo, string blobName,
        bool decrypt = false, CancellationToken cancellationToken = default);
    Task<Stream> StartDownloadBlobStreamAsync(Uri sasUri,
        bool decrypt = false, CancellationToken cancellationToken = default);

    // Delete
    Task DeleteBlobAsync(ContainerInfo containerInfo, string blobName,
        CancellationToken cancellationToken = default);
    Task DeleteBlobAsync(Uri sasUri, CancellationToken cancellationToken = default);
}
```

### Supporting Types

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

## Concrete Repository

```csharp
namespace {Project}.Infrastructure.Repositories;

public class {Project}BlobRepository : BlobRepositoryBase, I{Project}BlobRepository
{
    public {Project}BlobRepository(
        ILogger<{Project}BlobRepository> logger,
        IOptions<{Project}BlobRepositorySettings> settings,
        IAzureClientFactory<BlobServiceClient> clientFactory)
        : base(logger, settings, clientFactory)
    {
    }
}

public interface I{Project}BlobRepository : IBlobRepository { }
```

### Settings

```csharp
namespace {Project}.Infrastructure.Repositories;

public class {Project}BlobRepositorySettings : BlobRepositorySettingsBase { }
```

`BlobRepositorySettingsBase` provides:

```csharp
public abstract class BlobRepositorySettingsBase
{
    public string BlobServiceClientName { get; set; } = null!;
}
```

---

## Configuration

### appsettings.json

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

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "BlobStorage1": "UseDevelopmentStorage=true"
  }
}
```

> `UseDevelopmentStorage=true` connects to the Azurite local emulator.

---

## DI Registration (in Bootstrapper)

```csharp
private static void AddBlobStorageServices(IServiceCollection services, IConfiguration config)
{
    // Register named BlobServiceClient via IAzureClientFactory
    services.AddAzureClients(builder =>
    {
        builder.AddBlobServiceClient(config.GetConnectionString("BlobStorage1")!)
            .WithName("{Project}BlobClient");
    });

    // Bind settings
    services.Configure<{Project}BlobRepositorySettings>(
        config.GetSection("{Project}BlobRepositorySettings"));

    // Register repository
    services.AddScoped<I{Project}BlobRepository, {Project}BlobRepository>();
}
```

---

## Service Layer Usage

### File Upload Service

```csharp
public class AttachmentService(
    I{Project}BlobRepository blobRepo,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    private static readonly ContainerInfo AttachmentsContainer = new()
    {
        ContainerName = "attachments",
        CreateContainerIfNotExist = true
    };

    public async Task<Result<string>> UploadAsync(
        string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var blobName = $"{Guid.NewGuid()}/{fileName}";

        await blobRepo.UploadBlobStreamAsync(
            AttachmentsContainer, blobName, content, contentType, cancellationToken: ct);

        logger.LogInformation("Uploaded blob {BlobName} to {Container}", blobName, AttachmentsContainer.ContainerName);
        return Result<string>.Success(blobName);
    }

    public async Task<Result<Stream>> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var stream = await blobRepo.StartDownloadBlobStreamAsync(
            AttachmentsContainer, blobName, cancellationToken: ct);
        return Result<Stream>.Success(stream);
    }

    public async Task<Result> DeleteAsync(string blobName, CancellationToken ct = default)
    {
        await blobRepo.DeleteBlobAsync(AttachmentsContainer, blobName, cancellationToken: ct);
        return Result.Success();
    }
}
```

### SAS URI Pattern (Client-Side Upload/Download)

```csharp
public async Task<Result<Uri>> GenerateUploadUriAsync(
    string blobName, CancellationToken ct = default)
{
    var uri = await blobRepo.GenerateBlobSasUriAsync(
        AttachmentsContainer, blobName,
        BlobSasPermissions.Write | BlobSasPermissions.Create,
        DateTimeOffset.UtcNow.AddMinutes(15),
        cancellationToken: ct);

    return uri is not null
        ? Result<Uri>.Success(uri)
        : Result<Uri>.Failure("Unable to generate SAS URI. Client must use shared key credentials.");
}
```

### Distributed Lock Pattern

Use blob leases for cross-instance coordination:

```csharp
public async Task<Result<ProcessingResult>> ProcessWithLockAsync(CancellationToken ct = default)
{
    var result = await blobRepo.DistributedLockExecuteAsync<ProcessingResult>(
        new ContainerInfo { ContainerName = "locks", CreateContainerIfNotExist = true },
        leaseBlobName: "order-processing-lock",
        funcLocked: async () =>
        {
            // Only one instance executes this at a time
            return await ProcessOrdersAsync(ct);
        },
        cancellationToken: ct);

    return result is not null
        ? Result<ProcessingResult>.Success(result)
        : Result<ProcessingResult>.Failure("Could not acquire distributed lock.");
}
```

---

## Aspire Integration

In `AppHost/Program.cs`:

```csharp
var storage = builder.AddAzureStorage("AzureStorage")
    .RunAsEmulator();  // Uses Azurite locally

var blobs = storage.AddBlobs("BlobStorage1");

var api = builder.AddProject<Projects.{Project}_Api>("{project}-api")
    .WithReference(blobs);
```

---

## Blob Naming Conventions

| Pattern | Example | Use Case |
|---------|---------|----------|
| `{tenantId}/{entityType}/{entityId}/{filename}` | `abc123/orders/guid/invoice.pdf` | Tenant-scoped entity attachments |
| `{guid}/{filename}` | `d4e5f6/photo.jpg` | Simple unique storage |
| `{yyyy}/{MM}/{dd}/{filename}` | `2026/02/19/report.csv` | Date-partitioned archives |
| `{entityType}/{guid}` | `avatars/user-guid` | Type-organized content |

---

## Verification

After generating Blob Storage code, confirm:

- [ ] Repository inherits `BlobRepositoryBase` with project-specific settings
- [ ] Settings class inherits `BlobRepositorySettingsBase` with `BlobServiceClientName`
- [ ] DI registers named `BlobServiceClient` via `IAzureClientFactory<BlobServiceClient>`
- [ ] `ContainerInfo` objects define container names, access level, and auto-create behavior
- [ ] SAS URI generation uses scoped permissions and short expiry (15-60 minutes)
- [ ] Streams are properly disposed after download (use `await using`)
- [ ] Connection string uses `UseDevelopmentStorage=true` for local Azurite
- [ ] Cross-references: Aspire resource matches connection string name; IaC provisions storage account
