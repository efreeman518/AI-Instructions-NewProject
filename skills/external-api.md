# External API Integration

## Overview

When the solution needs to call third-party or partner REST APIs, each external service gets its own **Infrastructure project** (`{Project}.Infrastructure.{ServiceName}`) containing a Refit interface, request/response DTOs, settings, and resilience configuration. This keeps external contracts isolated from domain/application code and makes each integration independently testable and replaceable.

## Prerequisites

- [solution-structure.md](solution-structure.md) — `Infrastructure.{ExternalService}` folder convention
- [bootstrapper.md](bootstrapper.md) — DI registration pattern
- [configuration.md](configuration.md) — appsettings and Options pattern
- [aspire.md](aspire.md) — ServiceDefaults already applies a standard resilience handler to all `HttpClient` instances

## When to Use

- The domain inputs specify `externalApis` with one or more third-party service integrations.
- The solution needs to call partner/vendor REST APIs (payment processors, shipping providers, CRM systems, etc.).
- Any HTTP integration that is **not** an Azure SDK client (those use `IAzureClientFactory<T>` — see other infrastructure skills).

## Key Principles

1. **One Infrastructure project per external service** — `{Project}.Infrastructure.{ServiceName}` (e.g., `Infrastructure.Stripe`, `Infrastructure.Twilio`).
2. **Refit for typed HTTP clients** — declare the API contract as a C# interface; Refit generates the `HttpClient` implementation.
3. **.NET resilience at startup** — configure retry, circuit breaker, and timeout policies per-client using `Microsoft.Extensions.Http.Resilience`.
4. **Application layer owns the abstraction** — a service interface in `Application.Contracts` wraps the Refit client, keeping the application layer independent of the external API's shape.
5. **No raw `HttpClient` usage** — all HTTP calls flow through Refit interfaces with resilience pipelines attached.

## Project Structure

```
src/Infrastructure/
└── {Project}.Infrastructure.{ServiceName}/
    ├── Infrastructure.{ServiceName}.csproj
    ├── I{ServiceName}Api.cs                # Refit interface — external API contract
    ├── {ServiceName}Service.cs             # Wrapper implementing application-layer interface
    ├── {ServiceName}Settings.cs            # Strongly-typed settings (base URL, API key, timeouts)
    ├── ServiceCollectionExtensions.cs      # DI + Refit + resilience registration
    ├── Models/
    │   ├── {Operation}Request.cs           # Request DTOs matching external API shape
    │   ├── {Operation}Response.cs          # Response DTOs matching external API shape
    │   └── {ServiceName}ErrorResponse.cs   # Error response DTO for structured error handling
    └── Handlers/
        └── {ServiceName}AuthHandler.cs     # DelegatingHandler for API key / OAuth token injection
```

### Package References

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Application\{Project}.Application.Contracts\{Project}.Application.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Refit.HttpClientFactory" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
  </ItemGroup>
</Project>
```

> Add versions to `Directory.Packages.props` — not in the csproj.

## Refit Interface

The Refit interface declares the external API's HTTP contract. Keep it close to the provider's actual REST API shape — do **not** adapt it to your domain here (the wrapper service does that).

```csharp
using Refit;

namespace {Project}.Infrastructure.{ServiceName};

/// <summary>
/// Refit contract for {ServiceName} REST API.
/// This interface maps 1:1 to the provider's API endpoints.
/// </summary>
public interface I{ServiceName}Api
{
    [Post("/v1/charges")]
    Task<CreateChargeResponse> CreateChargeAsync(
        [Body] CreateChargeRequest request,
        CancellationToken ct = default);

    [Get("/v1/charges/{chargeId}")]
    Task<ChargeResponse> GetChargeAsync(
        string chargeId,
        CancellationToken ct = default);

    [Get("/v1/charges")]
    Task<ListChargesResponse> ListChargesAsync(
        [Query] int limit = 10,
        [Query] string? startingAfter = null,
        CancellationToken ct = default);

    [Post("/v1/refunds")]
    Task<RefundResponse> CreateRefundAsync(
        [Body] CreateRefundRequest request,
        CancellationToken ct = default);
}
```

### Refit Conventions

| Pattern | Usage |
|---------|-------|
| `[Get]`, `[Post]`, `[Put]`, `[Delete]`, `[Patch]` | HTTP method + relative path |
| `[Body]` | Serialize parameter as JSON request body |
| `[Query]` | Append as query string parameter |
| `[Header("X-Custom")]` | Send parameter as HTTP header |
| `[Authorize("Bearer")]` | Attach Authorization header (or use `DelegatingHandler`) |
| `[AliasAs("api_key")]` | Map C# property name to API's expected name |
| Return `ApiResponse<T>` | Access status code, headers, and content together |
| Return `IApiResponse<T>` | Same as above but does **not** throw on non-success status codes |

## Request / Response Models

Keep these in a `Models/` subfolder within the infrastructure project. They model the **external API's shape**, not your domain.

```csharp
namespace {Project}.Infrastructure.{ServiceName}.Models;

// — Request DTOs —

public record CreateChargeRequest
{
    [JsonPropertyName("amount")]
    public int AmountInCents { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "usd";

    [JsonPropertyName("source")]
    public string PaymentSourceToken { get; init; } = null!;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }
}

// — Response DTOs —

public record ChargeResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("amount")]
    public int AmountInCents { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    [JsonPropertyName("created")]
    public long CreatedUnixTimestamp { get; init; }

    [JsonPropertyName("failure_message")]
    public string? FailureMessage { get; init; }
}

// — Error DTO —

public record {ServiceName}ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorDetail? Error { get; init; }

    public record ErrorDetail
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = null!;

        [JsonPropertyName("message")]
        public string Message { get; init; } = null!;

        [JsonPropertyName("code")]
        public string? Code { get; init; }
    }
}
```

### Naming Rules

- Request/response record names match the **operation**, not your domain entity (e.g., `CreateChargeRequest`, not `CreatePaymentRequest`).
- Use `[JsonPropertyName]` when the external API uses snake_case or non-standard names.
- Use `init` properties for immutability.

## Settings

```csharp
namespace {Project}.Infrastructure.{ServiceName};

public class {ServiceName}Settings
{
    public const string ConfigSectionName = "{ServiceName}";

    public string BaseUrl { get; set; } = null!;
    public string ApiKey { get; set; } = null!;

    // Optional per-client resilience overrides
    public int RetryCount { get; set; } = 3;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 30;
}
```

### appsettings.json

```json
{
  "{ServiceName}": {
    "BaseUrl": "https://api.{servicename}.com",
    "ApiKey": "",
    "RetryCount": 3,
    "CircuitBreakerThreshold": 5,
    "TimeoutSeconds": 30
  }
}
```

> **Secrets:** Never commit API keys. Use User Secrets locally and Key Vault / App Configuration references in deployed environments. See [configuration.md](configuration.md).

## Auth Handler

Use a `DelegatingHandler` to inject authentication headers. This keeps auth logic out of the Refit interface and business code.

```csharp
using Microsoft.Extensions.Options;

namespace {Project}.Infrastructure.{ServiceName}.Handlers;

/// <summary>
/// Injects the API key (or Bearer token) into every outgoing request.
/// </summary>
public class {ServiceName}AuthHandler(
    IOptions<{ServiceName}Settings> settings) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // API key in header (common pattern — adapt to provider's auth scheme)
        request.Headers.Add("Authorization", $"Bearer {settings.Value.ApiKey}");

        return await base.SendAsync(request, cancellationToken);
    }
}
```

### OAuth Token Relay Variant

For APIs that require OAuth client-credentials tokens, replace the static API key with a token acquisition flow:

```csharp
public class {ServiceName}OAuthHandler(
    IOptions<{ServiceName}Settings> settings,
    IHttpClientFactory httpClientFactory) : DelegatingHandler
{
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_cachedToken is null || DateTimeOffset.UtcNow >= _tokenExpiry)
        {
            await AcquireTokenAsync(cancellationToken);
        }

        request.Headers.Authorization = new("Bearer", _cachedToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task AcquireTokenAsync(CancellationToken ct)
    {
        // Use a named HttpClient without the auth handler to avoid recursion
        var tokenClient = httpClientFactory.CreateClient("{ServiceName}-token");
        // Token endpoint call... set _cachedToken and _tokenExpiry
    }
}
```

## DI + Refit + Resilience Registration

This is the core of the pattern — a single extension method that wires up Refit, the auth handler, and .NET resilience policies.

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Refit;

namespace {Project}.Infrastructure.{ServiceName};

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Add{ServiceName}(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Bind settings
        var section = configuration.GetSection({ServiceName}Settings.ConfigSectionName);
        services.Configure<{ServiceName}Settings>(section);
        var settings = section.Get<{ServiceName}Settings>()
            ?? throw new InvalidOperationException($"Missing config section: {{ServiceName}Settings.ConfigSectionName}}");

        // 2. Register auth handler
        services.AddTransient<{ServiceName}AuthHandler>();

        // 3. Register Refit client with auth handler + resilience
        services
            .AddRefitClient<I{ServiceName}Api>(new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer(new()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                })
            })
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(settings.BaseUrl);
                c.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            })
            .AddHttpMessageHandler<{ServiceName}AuthHandler>()
            .AddResilienceHandler("{ServiceName}", (builder, context) =>
            {
                // Retry with exponential backoff + jitter
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = settings.RetryCount,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(1),
                    ShouldHandle = static args => ValueTask.FromResult(
                        HttpClientResiliencePredicates.IsTransient(args.Outcome))
                });

                // Circuit breaker
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.5,
                    MinimumThroughput = settings.CircuitBreakerThreshold,
                    BreakDuration = TimeSpan.FromSeconds(15),
                    ShouldHandle = static args => ValueTask.FromResult(
                        HttpClientResiliencePredicates.IsTransient(args.Outcome))
                });

                // Request timeout (per-attempt, inside retries)
                builder.AddTimeout(TimeSpan.FromSeconds(10));
            });

        // 4. Register application-layer wrapper
        services.AddScoped<I{ServiceName}Service, {ServiceName}Service>();

        return services;
    }
}
```

### Resilience Strategy Explained

| Strategy | Purpose | Default |
|----------|---------|---------|
| **Retry** | Retries transient failures (5xx, 408, network errors) with exponential backoff + jitter | 3 attempts, 1s base delay |
| **Circuit Breaker** | Prevents cascading failures by short-circuiting calls when error ratio exceeds threshold | 50% failure over 30s window → break 15s |
| **Timeout** | Per-attempt timeout (shorter than the overall `HttpClient.Timeout`) | 10s per attempt |

> **Global vs per-client resilience:** Aspire `ServiceDefaults` applies `AddStandardResilienceHandler()` to **all** `HttpClient` instances globally. When you add `.AddResilienceHandler(...)` per-client (as above), it **replaces** the global handler for that named client, giving you full control over retry/breaker settings. Use per-client when the external API has specific SLA/rate-limit requirements.

### Resilience Customization Tips

```csharp
// Rate-limit aware retry — respect Retry-After header
builder.AddRetry(new HttpRetryStrategyOptions
{
    MaxRetryAttempts = 3,
    BackoffType = DelayBackoffType.Exponential,
    UseJitter = true,
    DelayGenerator = static args =>
    {
        if (args.Outcome.Result?.Headers.RetryAfter?.Delta is { } retryAfter)
            return ValueTask.FromResult<TimeSpan?>(retryAfter);
        return ValueTask.FromResult<TimeSpan?>(null); // fall back to backoff
    }
});

// Hedging — send parallel request to backup endpoint after delay
builder.AddHedging(new HttpHedgingStrategyOptions
{
    MaxHedgedAttempts = 1,
    Delay = TimeSpan.FromSeconds(2),
    ActionGenerator = static args =>
    {
        // Clone request to backup URL
        return () => /* send to fallback endpoint */;
    }
});
```

## Wrapper Service

The wrapper service implements an interface from `Application.Contracts` and translates between external API models and your domain/application models. This is where you handle error mapping, model conversion, and logging.

### Application.Contracts Interface

```csharp
namespace {Project}.Application.Contracts.Services;

public interface I{ServiceName}Service
{
    Task<DomainResult<PaymentResultDto>> ProcessPaymentAsync(
        PaymentRequestDto request, CancellationToken ct = default);

    Task<DomainResult<PaymentResultDto>> GetPaymentAsync(
        string paymentId, CancellationToken ct = default);

    Task<DomainResult> RefundPaymentAsync(
        string paymentId, int amountInCents, CancellationToken ct = default);
}
```

### Infrastructure Implementation

```csharp
using Microsoft.Extensions.Logging;
using Refit;

namespace {Project}.Infrastructure.{ServiceName};

public class {ServiceName}Service(
    I{ServiceName}Api api,
    ILogger<{ServiceName}Service> logger) : I{ServiceName}Service
{
    public async Task<DomainResult<PaymentResultDto>> ProcessPaymentAsync(
        PaymentRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var externalRequest = new CreateChargeRequest
            {
                AmountInCents = request.AmountInCents,
                Currency = request.Currency,
                PaymentSourceToken = request.SourceToken,
                Description = request.Description,
                Metadata = new Dictionary<string, string>
                {
                    ["orderId"] = request.OrderId.ToString()
                }
            };

            var response = await api.CreateChargeAsync(externalRequest, ct);

            return DomainResult<PaymentResultDto>.Success(new PaymentResultDto
            {
                ExternalId = response.Id,
                Status = MapStatus(response.Status),
                AmountInCents = response.AmountInCents
            });
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            var error = await ex.GetContentAsAsync<{ServiceName}ErrorResponse>();
            logger.LogWarning("Payment declined: {Message}", error?.Error?.Message);
            return DomainResult<PaymentResultDto>.Failure(
                error?.Error?.Message ?? "Payment was declined.");
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "External API error calling {ServiceName}: {StatusCode}",
                nameof({ServiceName}), ex.StatusCode);
            return DomainResult<PaymentResultDto>.Failure(
                "An error occurred communicating with the payment provider.");
        }
    }

    public async Task<DomainResult<PaymentResultDto>> GetPaymentAsync(
        string paymentId, CancellationToken ct = default)
    {
        try
        {
            var response = await api.GetChargeAsync(paymentId, ct);
            return DomainResult<PaymentResultDto>.Success(new PaymentResultDto
            {
                ExternalId = response.Id,
                Status = MapStatus(response.Status),
                AmountInCents = response.AmountInCents
            });
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Failed to get charge {ChargeId}", paymentId);
            return DomainResult<PaymentResultDto>.Failure(
                "Failed to retrieve payment status.");
        }
    }

    public async Task<DomainResult> RefundPaymentAsync(
        string paymentId, int amountInCents, CancellationToken ct = default)
    {
        try
        {
            await api.CreateRefundAsync(new CreateRefundRequest
            {
                ChargeId = paymentId,
                AmountInCents = amountInCents
            }, ct);

            return DomainResult.Success();
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Failed to refund charge {ChargeId}", paymentId);
            return DomainResult.Failure("Refund request failed.");
        }
    }

    private static string MapStatus(string externalStatus) => externalStatus switch
    {
        "succeeded" => "Completed",
        "pending" => "Processing",
        "failed" => "Failed",
        _ => "Unknown"
    };
}
```

### Error Handling Strategy

| Refit Exception | Meaning | Recommended Handling |
|-----------------|---------|---------------------|
| `ApiException` (4xx) | Client error — bad request, auth failure, validation | Map to `DomainResult.Failure` with user-friendly message; log details |
| `ApiException` (5xx) | Server error — provider outage | Resilience pipeline retries automatically; surface generic failure after exhaustion |
| `HttpRequestException` | Network-level failure | Resilience pipeline handles; surface generic failure |
| `TaskCanceledException` | Timeout or cancellation | Distinguish via `ct.IsCancellationRequested`; timeout → retry; cancellation → propagate |

> **Never expose external API error details to end users.** Log the full `ApiException` (including response body) for diagnostics; return a sanitized `DomainResult.Failure` message.

## Bootstrapper Registration

Register each external service in the Bootstrapper:

```csharp
public static IServiceCollection RegisterInfrastructureServices(
    this IServiceCollection services, IConfiguration config)
{
    // ... existing registrations ...

    // External API integrations
    services.Add{ServiceName}(config);

    return services;
}
```

### Dependency Flow

```
Application.Contracts  ← defines I{ServiceName}Service
Infrastructure.{ServiceName} ← implements I{ServiceName}Service, owns I{ServiceName}Api (Refit)
Bootstrapper ← references Infrastructure.{ServiceName}, registers DI
Api / Functions / Scheduler ← uses I{ServiceName}Service via injection
```

## Testing

### Unit Testing the Wrapper Service

Mock the Refit interface to test mapping and error handling:

```csharp
[TestClass]
public class {ServiceName}ServiceTests
{
    private readonly Mock<I{ServiceName}Api> _mockApi = new();
    private readonly {ServiceName}Service _sut;

    public {ServiceName}ServiceTests()
    {
        _sut = new {ServiceName}Service(
            _mockApi.Object,
            Mock.Of<ILogger<{ServiceName}Service>>());
    }

    [TestMethod]
    public async Task ProcessPayment_Success_ReturnsMappedResult()
    {
        _mockApi.Setup(a => a.CreateChargeAsync(It.IsAny<CreateChargeRequest>(), default))
            .ReturnsAsync(new ChargeResponse
            {
                Id = "ch_123",
                AmountInCents = 5000,
                Status = "succeeded"
            });

        var result = await _sut.ProcessPaymentAsync(new PaymentRequestDto
        {
            AmountInCents = 5000,
            Currency = "usd",
            SourceToken = "tok_visa",
            OrderId = Guid.NewGuid()
        });

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("ch_123", result.Value!.ExternalId);
        Assert.AreEqual("Completed", result.Value.Status);
    }

    [TestMethod]
    public async Task ProcessPayment_Declined_ReturnsFailure()
    {
        _mockApi.Setup(a => a.CreateChargeAsync(It.IsAny<CreateChargeRequest>(), default))
            .ThrowsAsync(await ApiException.Create(
                new HttpRequestMessage(),
                HttpMethod.Post,
                new HttpResponseMessage(System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    Content = JsonContent.Create(new { error = new { message = "Card declined" } })
                },
                new RefitSettings()));

        var result = await _sut.ProcessPaymentAsync(new PaymentRequestDto
        {
            AmountInCents = 5000,
            SourceToken = "tok_declined"
        });

        Assert.IsTrue(result.IsFailure);
        StringAssert.Contains(result.ErrorMessage, "declined");
    }
}
```

### Integration Testing

Use `WebApplicationFactory` with a mock Refit client to test the full request pipeline without hitting the real API:

```csharp
// In test setup, replace the Refit client registration
services.RemoveAll<I{ServiceName}Api>();
services.AddSingleton(_mockApi.Object);
```

## Multiple External APIs

Each external API gets its own Infrastructure project. Register them all in the Bootstrapper:

```csharp
public static IServiceCollection RegisterInfrastructureServices(
    this IServiceCollection services, IConfiguration config)
{
    services.AddStripe(config);    // Infrastructure.Stripe
    services.AddTwilio(config);    // Infrastructure.Twilio
    services.AddShipStation(config); // Infrastructure.ShipStation
    return services;
}
```

This keeps each integration isolated — different auth patterns, resilience settings, models, and deployment/testing lifecycles.

## Verification

After scaffolding an external API integration, confirm:

- [ ] `Infrastructure.{ServiceName}` project exists with Refit interface, models, settings, auth handler, and DI extension
- [ ] `Directory.Packages.props` has `Refit.HttpClientFactory` and `Microsoft.Extensions.Http.Resilience` versions
- [ ] Settings class has `BaseUrl`, auth credentials, and resilience parameters
- [ ] Auth handler injects credentials without leaking into Refit interface or business code
- [ ] Resilience handler is configured per-client with retry, circuit breaker, and timeout
- [ ] Wrapper service implements an `Application.Contracts` interface, not the Refit interface directly
- [ ] Wrapper maps external models → application DTOs and returns `DomainResult<T>`
- [ ] `ApiException` is caught and logged; external error details are **not** surfaced to end users
- [ ] Bootstrapper registers the service via `services.Add{ServiceName}(config)`
- [ ] Unit tests mock `I{ServiceName}Api` to verify mapping and error handling
- [ ] Architecture test confirms Application layer does not reference `Refit` or `Infrastructure.{ServiceName}` directly
