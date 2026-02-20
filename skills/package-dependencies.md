# Package Dependencies

## Overview

The solution depends on shared infrastructure NuGet packages (`Package.Infrastructure.*`) that provide base classes, utilities, and cross-cutting concerns. These are private/internal packages — not available on nuget.org. This document defines their key types and APIs so generated code is correct.

> **Reference implementation:** See `sampleapp/src/Directory.Packages.props` for centralized package version management, and `sampleapp/src/nuget.config` for NuGet feed configuration including private feeds.

### Custom NuGet Feeds & Package Restore

The user **must** declare all custom NuGet feeds in `domain-inputs.schema.md` → `customNugetFeeds` at the start of the project. Without all feeds configured in `nuget.config`, the solution will fail to restore.

**Rules:**
1. `nuget.config` must include `nuget.org` **plus** every feed listed in `customNugetFeeds`.
2. After adding any new `<PackageReference>` to a project or `Directory.Packages.props`, **always update to the latest stable versions** from `nuget.org` and all configured custom feeds. Verify with `dotnet restore` after each change.
3. If a restore fails due to a missing package source, check `nuget.config` for the correct feed URL and authentication credentials.
4. Use `dotnet list package --outdated` to identify stale versions after initial scaffold, then update `Directory.Packages.props` to latest stable.
5. This applies to **every** phase of scaffolding — not just initial setup. Any time new packages are added (e.g., adding Aspire hosting packages, testing packages, Azure SDK packages), update all package versions to latest stable.

---

## Package.Infrastructure.Domain

Base classes and result patterns for domain entities.

### EntityBase

```csharp
public abstract class EntityBase
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public byte[] RowVersion { get; set; } = [];
}
```

### DomainResult\<T\>

Railway-oriented result type for domain operations:

```csharp
public class DomainResult<T>
{
    public static DomainResult<T> Success(T value);
    public static DomainResult<T> Failure(string error);
    public static DomainResult<T> Failure(IEnumerable<DomainError> errors);

    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }  // combined error string

    public DomainResult<TNext> Bind<TNext>(Func<T, DomainResult<TNext>> func);
    public DomainResult<TNext> Map<TNext>(Func<T, TNext> func);
    public DomainResult<T> Map(T value);  // replace value, keep success/failure state
}

public class DomainResult
{
    public static DomainResult Success();
    public static DomainResult Failure(string error);
    public static DomainResult Failure(IEnumerable<string> errors);
    public static DomainResult Combine(params DomainResult[] results);

    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public string? ErrorMessage { get; }
}
```

### DomainError

```csharp
public class DomainError
{
    public string Message { get; }
    public static DomainError Create(string message);
}
```

---

## Package.Infrastructure.Domain.Contracts

Interfaces for domain entity contracts.

### ITenantEntity\<T\>

```csharp
public interface ITenantEntity<TTenantId>
{
    TTenantId TenantId { get; }
}
```

### IEntityBaseDto

```csharp
public interface IEntityBaseDto
{
    Guid? Id { get; set; }
}
```

---

## Package.Infrastructure.Data

EF Core base classes, repository pattern, and interceptors.

### DbContextBase\<TAuditId, TTenantId\>

```csharp
public abstract class DbContextBase<TAuditId, TTenantId>(DbContextOptions options) : DbContext(options)
{
    // Provides:
    // - OnModelCreating base (calls convention configuration)
    // - BuildTenantFilter(Type clrType) — returns Expression for tenant query filter
    // - Tenant context from IRequestContext
}
```

### RepositoryBase\<TContext, TAuditId, TTenantId\>

```csharp
public abstract class RepositoryBase<TContext, TAuditId, TTenantId>(TContext dbContext)
    where TContext : DbContext
{
    protected TContext DB { get; }  // the underlying DbContext

    // === Core CRUD ===
    Task<T?> GetEntityAsync<T>(
        bool tracking = true,
        Expression<Func<T, bool>>? filter = null,
        SplitQueryThresholdOptions? splitQueryThresholdOptions = null,
        Expression<Func<IQueryable<T>, IIncludableQueryable<T, object?>>>[]? includes = null,
        CancellationToken cancellationToken = default) where T : class;

    void Create<T>(ref T entity) where T : class;
    void UpdateFull<T>(ref T entity) where T : class;
    void Delete<T>(T entity) where T : class;
    Task DeleteAsync<T>(CancellationToken ct, params object[] keyValues) where T : class;

    Task<int> SaveChangesAsync(OptimisticConcurrencyWinner winner = OptimisticConcurrencyWinner.Throw,
        CancellationToken ct = default);

    // === Query/Projection ===
    Task<PagedResponse<TDto>> QueryPageProjectionAsync<T, TDto>(
        Expression<Func<T, TDto>> projector,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) where T : class;

    Task<PagedResponse<T>> QueryPageAsync<T>(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) where T : class;
}
```

### EntityBaseConfiguration\<T\>

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

### Supporting Types

```csharp
public enum OptimisticConcurrencyWinner { Throw, ClientWins, StoreWins }

public class SplitQueryThresholdOptions
{
    public static SplitQueryThresholdOptions Default { get; }
    public int Threshold { get; set; }
}

public enum RelatedDeleteBehavior { None, RelationshipOnly, RelationshipAndEntity }

// AuditInterceptor — SaveChangesInterceptor that sets CreatedDate, CreatedBy,
// UpdatedDate, UpdatedBy on entities implementing IAuditable.
// Registered on DbContextTrxn only.

// DbContextScopedFactory — creates scoped DbContext instances from a pooled factory.
```

---

## Package.Infrastructure.Common

Shared utilities, request/response wrappers, and result pattern.

### Result\<T\>

```csharp
public class Result<T>
{
    public static Result<T> Success(T value);
    public static Result<T> Failure(string error);
    public static Result<T> Failure(IEnumerable<string> errors);
    public static Result<T> None();  // not found (null-safe)

    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public bool IsNone { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyList<string>? Errors { get; }

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<IEnumerable<string>, TResult> onFailure,
        Func<TResult>? onNone = null);
}

public class Result
{
    public static Result Success();
    public static Result Failure(string error);
    public static Result Combine(params Result[] results);

    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public string? ErrorMessage { get; }

    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<IEnumerable<string>, TResult> onFailure);
}
```

### Request/Response Types

```csharp
public class SearchRequest<TFilter>
{
    public TFilter? Filter { get; set; }
    public int PageSize { get; set; } = 10;
    public int Page { get; set; } = 1;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }  // "asc" or "desc"
}

public class PagedResponse<T>
{
    public List<T> Data { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int Page { get; set; }
}

public record DefaultRequest<T>
{
    public T Item { get; set; } = default!;
}

public record DefaultResponse<T>
{
    public T? Item { get; set; }
    public TenantInfoDto? TenantInfo { get; set; }
}
```

### Lookup Types

```csharp
public class StaticItem<TId, TParentId>
{
    public TId Id { get; set; }
    public TParentId ParentId { get; set; }
    public string Label { get; set; } = null!;

    public StaticItem(TId id, string label, TParentId parentId);
}

public class StaticList<T>
{
    public List<T> Items { get; set; } = [];
}
```

### CollectionUtility

```csharp
public static class CollectionUtility
{
    // Synchronizes an existing collection with incoming data.
    // See updater-template.md for full DomainResult-returning overload.
    public static void SyncCollectionWithResult<TExisting, TIncoming, TKey>(
        IReadOnlyCollection<TExisting> existing,
        IReadOnlyCollection<TIncoming> incoming,
        Func<TExisting, TKey> existingKey,
        Func<TIncoming, TKey?> incomingKey,
        Action<TExisting, TIncoming> update,
        Func<TIncoming, TExisting?> add,
        Action<TExisting> remove)
        where TKey : struct;
}
```

### IRequestContext

```csharp
public interface IRequestContext<TAuditId, TTenantId>
{
    TAuditId? AuditId { get; }
    TTenantId? TenantId { get; }
    IReadOnlyCollection<string> Roles { get; }
}

// Concrete: RequestContext — populated from JWT claims by middleware.
```

### ProblemDetailsHelper

```csharp
public static class ProblemDetailsHelper
{
    public static ProblemDetails BuildProblemDetailsResponse(
        int? statusCodeOverride = null,
        string? message = null,
        string? traceId = null,
        bool includeStackTrace = false);

    public static ProblemDetails BuildProblemDetailsResponseMultiple(
        IEnumerable<string>? messages = null,
        int? statusCodeOverride = null,
        string? traceId = null,
        bool includeStackTrace = false);
}
```

---

## Package.Infrastructure.Host

Hosting utilities.

### StaticLogging

```csharp
public static class StaticLogging
{
    public static ILogger Logger { get; set; }  // Set at startup for pre-DI logging
}
```

---

## Package.Infrastructure.Common — Additional Types

### ITenantBoundaryValidator

```csharp
public interface ITenantBoundaryValidator
{
    Result EnsureTenantBoundary(
        ILogger logger,
        Guid? requestTenantId,
        IReadOnlyCollection<string> requestRoles,
        Guid entityTenantId,
        string operationName,
        string entityTypeName,
        Guid? entityId = null);

    Result PreventTenantChange(
        ILogger logger,
        Guid existingTenantId,
        Guid requestedTenantId,
        string entityTypeName,
        Guid entityId);
}
```

### IInternalMessageBus

```csharp
public interface IInternalMessageBus
{
    Task PublishAsync<T>(T message, CancellationToken ct = default);
    void AutoRegisterHandlers(IServiceCollection services, params Assembly[] assemblies);
}
```

### IEntityCacheProvider

```csharp
public interface IEntityCacheProvider
{
    Task<TenantInfoDto?> GetTenantInfoAsync(Guid tenantId, CancellationToken ct);
    Task<T?> GetOrSetEntityAsync<T>(Guid[] ids, CancellationToken ct) where T : EntityBase;
    Task WarmupAsync(CancellationToken ct);
}
```

---

## Package.Infrastructure.CosmosDb

Cosmos DB repository pattern with partition key support. See [cosmosdb-data.md](cosmosdb-data.md) for full scaffolding guide.

### CosmosDbEntity

```csharp
public abstract class CosmosDbEntity : EntityBase
{
    public string PartitionKey { get; set; } = null!;
}
```

### ICosmosDbRepository

```csharp
public interface ICosmosDbRepository
{
    Task<T?> GetItemAsync<T>(string id, string partitionKey, CancellationToken ct = default) where T : CosmosDbEntity;
    Task<T> CreateItemAsync<T>(T entity, CancellationToken ct = default) where T : CosmosDbEntity;
    Task<T> UpsertItemAsync<T>(T entity, CancellationToken ct = default) where T : CosmosDbEntity;
    Task DeleteItemAsync<T>(string id, string partitionKey, CancellationToken ct = default) where T : CosmosDbEntity;

    Task<PagedResponse<T>> QueryPageAsync<T>(
        Expression<Func<T, bool>>? filter = null,
        int pageSize = 10, string? continuationToken = null,
        CancellationToken ct = default) where T : CosmosDbEntity;

    IAsyncEnumerable<T> StreamQueryAsync<T>(
        QueryDefinition query, string? partitionKey = null,
        CancellationToken ct = default) where T : CosmosDbEntity;
}
```

### CosmosDbRepositoryBase

```csharp
public abstract class CosmosDbRepositoryBase : ICosmosDbRepository
{
    // Constructor: IAzureClientFactory<CosmosClient>, IOptions<TSettings>
    // Resolves named CosmosClient, database, container from settings
    // Subclasses override ContainerName if needed
}
```

### CosmosDbRepositorySettingsBase

```csharp
public class CosmosDbRepositorySettingsBase
{
    public string ClientName { get; set; } = null!;   // IAzureClientFactory named client
    public string DatabaseName { get; set; } = null!;
    public string ContainerName { get; set; } = null!;
}
```

---

## Package.Infrastructure.Storage

Azure Blob Storage repository. See [blob-storage.md](blob-storage.md) for full scaffolding guide.

### IBlobRepository

```csharp
public interface IBlobRepository
{
    Task<BlobProperties?> UploadAsync(string containerName, string blobName, Stream content,
        string? contentType = null, IDictionary<string, string>? metadata = null,
        CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default);
    Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken ct = default);
    Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken ct = default);

    Task<Uri> GetSasUriAsync(string containerName, string blobName,
        BlobSasPermissions permissions, TimeSpan expiry, CancellationToken ct = default);

    Task<BlobContainerClient> GetContainerClientAsync(string containerName, CancellationToken ct = default);

    // Distributed lock
    Task<string?> AcquireLeaseAsync(string containerName, string blobName,
        TimeSpan duration, CancellationToken ct = default);
    Task ReleaseLeaseAsync(string containerName, string blobName,
        string leaseId, CancellationToken ct = default);
}
```

### BlobRepositoryBase

```csharp
public abstract class BlobRepositoryBase : IBlobRepository
{
    // Constructor: IAzureClientFactory<BlobServiceClient>, IOptions<TSettings>
}
```

### BlobRepositorySettingsBase

```csharp
public class BlobRepositorySettingsBase
{
    public string ClientName { get; set; } = null!;
    public List<ContainerInfo> Containers { get; set; } = [];
}

public class ContainerInfo
{
    public string ContainerName { get; set; } = null!;
    public ContainerPublicAccessType PublicAccessType { get; set; } = ContainerPublicAccessType.None;
}
```

---

## Package.Infrastructure.Table

Azure Table Storage repository. See [table-storage.md](table-storage.md) for full scaffolding guide.

### ITableRepository

```csharp
public interface ITableRepository
{
    Task<T?> GetEntityAsync<T>(string partitionKey, string rowKey,
        CancellationToken ct = default) where T : class, ITableEntity, new();
    Task UpsertEntityAsync<T>(T entity,
        TableUpdateMode mode = TableUpdateMode.Merge,
        CancellationToken ct = default) where T : class, ITableEntity, new();
    Task DeleteEntityAsync(string partitionKey, string rowKey,
        CancellationToken ct = default);

    Task<PagedResponse<T>> QueryPageAsync<T>(
        Expression<Func<T, bool>>? filter = null,
        int pageSize = 10, string? continuationToken = null,
        CancellationToken ct = default) where T : class, ITableEntity, new();

    AsyncPageable<T> QueryAsync<T>(
        Expression<Func<T, bool>>? filter = null,
        CancellationToken ct = default) where T : class, ITableEntity, new();
}
```

### TableRepositoryBase

```csharp
public abstract class TableRepositoryBase : ITableRepository
{
    // Constructor: IAzureClientFactory<TableServiceClient>, IOptions<TSettings>
    // Resolves named TableServiceClient and table from settings
}
```

### TableRepositorySettingsBase

```csharp
public class TableRepositorySettingsBase
{
    public string ClientName { get; set; } = null!;
    public string TableName { get; set; } = null!;
}
```

---

## Package.Infrastructure.Messaging

Service Bus, Event Grid, and Event Hub messaging abstractions. See [messaging.md](messaging.md) for full scaffolding guide.

### Service Bus — Sender

```csharp
public interface IServiceBusSender
{
    Task SendMessageAsync<T>(T message, string? sessionId = null,
        string? correlationId = null, IDictionary<string, object>? properties = null,
        CancellationToken ct = default);
    Task SendMessagesAsync<T>(IEnumerable<T> messages, CancellationToken ct = default);
    Task ScheduleMessageAsync<T>(T message, DateTimeOffset scheduledEnqueueTime,
        CancellationToken ct = default);
}

public abstract class ServiceBusSenderBase : IServiceBusSender
{
    // Constructor: IAzureClientFactory<ServiceBusClient>, IOptions<TSettings>
}
```

### Service Bus — Receiver / Processor

```csharp
public interface IServiceBusReceiver
{
    Task<IReadOnlyList<T>> ReceiveMessagesAsync<T>(int maxMessages = 10,
        TimeSpan? maxWaitTime = null, CancellationToken ct = default);
    Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken ct = default);
    Task AbandonMessageAsync(ServiceBusReceivedMessage message, CancellationToken ct = default);
    Task DeadLetterMessageAsync(ServiceBusReceivedMessage message,
        string reason, CancellationToken ct = default);
}

public abstract class ServiceBusProcessorBase : BackgroundService
{
    // Constructor: IAzureClientFactory<ServiceBusClient>, IOptions<TSettings>, IServiceProvider
    // Override: Task ProcessMessageAsync(ProcessMessageEventArgs args, CancellationToken ct)
    // Override: Task ProcessErrorAsync(ProcessErrorEventArgs args, CancellationToken ct)
}
```

### Event Grid — Publisher

```csharp
public interface IEventGridPublisher
{
    Task PublishEventAsync<T>(string eventType, string subject, T data,
        CancellationToken ct = default);
    Task PublishEventsAsync<T>(IEnumerable<EventGridEvent> events,
        CancellationToken ct = default);
}

public abstract class EventGridPublisherBase : IEventGridPublisher
{
    // Constructor: IAzureClientFactory<EventGridPublisherClient>, IOptions<TSettings>
}
```

### Event Hub — Producer / Processor

```csharp
public interface IEventHubProducer
{
    Task SendAsync<T>(T data, string? partitionKey = null, CancellationToken ct = default);
    Task SendBatchAsync<T>(IEnumerable<T> data, string? partitionKey = null,
        CancellationToken ct = default);
}

public abstract class EventHubProducerBase : IEventHubProducer
{
    // Constructor: IAzureClientFactory<EventHubProducerClient>, IOptions<TSettings>
}

public interface IEventHubProcessor
{
    Task StartProcessingAsync(CancellationToken ct = default);
    Task StopProcessingAsync(CancellationToken ct = default);
}

public abstract class EventHubProcessorBase : BackgroundService, IEventHubProcessor
{
    // Constructor: EventProcessorClient, IServiceProvider
    // Override: Task ProcessEventAsync(ProcessEventArgs args, CancellationToken ct)
    // Override: Task ProcessErrorAsync(ProcessErrorEventArgs args, CancellationToken ct)
}
```

---

## Package.Infrastructure.KeyVault

Azure Key Vault management and field-level encryption. See [keyvault.md](keyvault.md) for full scaffolding guide.

### IKeyVaultManager

```csharp
public interface IKeyVaultManager
{
    // Secrets
    Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default);
    Task SetSecretAsync(string secretName, string value, CancellationToken ct = default);
    Task DeleteSecretAsync(string secretName, CancellationToken ct = default);

    // Keys
    Task<KeyVaultKey> GetKeyAsync(string keyName, CancellationToken ct = default);
    Task<KeyVaultKey> CreateKeyAsync(string keyName, KeyType keyType, CancellationToken ct = default);

    // Certificates
    Task<KeyVaultCertificateWithPolicy> GetCertificateAsync(string certName, CancellationToken ct = default);
}
```

### IKeyVaultCryptoUtility

```csharp
public interface IKeyVaultCryptoUtility
{
    Task<string> EncryptAsync(string keyName, string plaintext, CancellationToken ct = default);
    Task<string> DecryptAsync(string keyName, string ciphertext, CancellationToken ct = default);
}
```

### KeyVaultManagerSettingsBase

```csharp
public class KeyVaultManagerSettingsBase
{
    public string VaultUri { get; set; } = null!;
    public string SecretClientName { get; set; } = null!;
    public string KeyClientName { get; set; } = null!;
    public string CertificateClientName { get; set; } = null!;
}
```

---

## Package.Infrastructure.Grpc

gRPC error interceptor pattern. See [grpc.md](grpc.md) for full scaffolding guide.

### Error Interceptors

```csharp
// Client-side — converts RpcException to standard error responses
public class ClientErrorInterceptor(ILogger<ClientErrorInterceptor> logger,
    IOptions<ErrorInterceptorSettings> settings) : Interceptor
{
    // Wraps all unary/streaming calls with try/catch → ProblemDetails
}

// Server-side — converts unhandled exceptions to RpcException with proper status codes
public class ServiceErrorInterceptor(ILogger<ServiceErrorInterceptor> logger,
    IOptions<ErrorInterceptorSettings> settings) : Interceptor
{
    // Maps exception types to gRPC status codes
}

public class ErrorInterceptorSettings
{
    public bool IncludeExceptionDetails { get; set; }  // true in Development
}
```

### DI Registration Extension

```csharp
public static class IServiceCollectionExtensions
{
    // Registers ClientErrorInterceptor or ServiceErrorInterceptor with options
    public static IServiceCollection AddGrpcErrorInterceptors(
        this IServiceCollection services, IConfiguration configuration);
}
```

---

## External API Integration Packages (Public NuGet)

These are **public NuGet packages** (not private infrastructure packages) used by `Infrastructure.{ServiceName}` projects for external API integration. See [external-api.md](external-api.md) for full scaffolding guide.

### Refit.HttpClientFactory

Generates `HttpClient` implementations from C# interfaces at compile time.

```csharp
// Registration — creates a named HttpClient backed by the Refit interface
services.AddRefitClient<I{ServiceName}Api>(new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(jsonOptions)
})
.ConfigureHttpClient(c => c.BaseAddress = new Uri(settings.BaseUrl))
.AddHttpMessageHandler<{ServiceName}AuthHandler>();
```

### Microsoft.Extensions.Http.Resilience

Provides `.AddResilienceHandler()` and `.AddStandardResilienceHandler()` for `IHttpClientBuilder`. Built on top of Polly v8.

```csharp
// Per-client resilience pipeline (replaces global StandardResilienceHandler for this client)
.AddResilienceHandler("{ServiceName}", (builder, context) =>
{
    builder.AddRetry(new HttpRetryStrategyOptions { /* ... */ });
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions { /* ... */ });
    builder.AddTimeout(TimeSpan.FromSeconds(10));
});
```

| Strategy | Class | Purpose |
|----------|-------|---------|
| Retry | `HttpRetryStrategyOptions` | Exponential backoff + jitter for transient failures |
| Circuit Breaker | `HttpCircuitBreakerStrategyOptions` | Short-circuit calls when failure ratio exceeds threshold |
| Timeout | `AddTimeout(TimeSpan)` | Per-attempt timeout (inside retries) |
| Hedging | `HttpHedgingStrategyOptions` | Parallel request to fallback endpoint after delay |

> Both packages must be versioned in `Directory.Packages.props`. They are public NuGet packages, not private.

---

## Verification

After setting up package dependencies, confirm:

- [ ] `nuget.config` includes both the private NuGet feed and `nuget.org`
- [ ] `Directory.Packages.props` manages all package versions centrally (projects use `<PackageReference>` without `Version`)
- [ ] `global.json` pins the .NET SDK version
- [ ] `Package.Infrastructure.*` packages are installed in the correct projects (Domain, Data, Common, Host, and any CosmosDb/Storage/Table/Messaging/KeyVault/Grpc packages where needed)
- [ ] No version conflicts between private packages and public NuGet packages
- [ ] All packages are updated to the **latest stable versions** from nuget.org and custom feeds after adding references
- [ ] Interface contracts from private packages (`IEntityCacheProvider`, `IInternalMessageBus`, `ICosmosDbRepository`, `IBlobRepository`, `ITableRepository`, `IServiceBusSender`, `IKeyVaultManager`, etc.) are implemented correctly
- [ ] Azure SDK client factories (`IAzureClientFactory<T>`) are registered with named clients for all infrastructure packages that require them
- [ ] Cross-references: Package versions match [solution-structure.md](solution-structure.md) dependency flow, Bootstrapper registers all package-provided interfaces
