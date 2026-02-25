# External API Integration

Use one infrastructure project per external API: `Infrastructure.{ServiceName}`.

## Structure

```text
src/Infrastructure/{Project}.Infrastructure.{ServiceName}/
  I{ServiceName}Api.cs
  {ServiceName}Service.cs
  {ServiceName}Settings.cs
  ServiceCollectionExtensions.cs
  Handlers/{ServiceName}AuthHandler.cs
  Models/*Request.cs
  Models/*Response.cs
```

Rule: Application layer depends on `I{ServiceName}Service` (contracts), not Refit types.

---

## Packages

```xml
<PackageReference Include="Refit.HttpClientFactory" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
```

Version all packages in `Directory.Packages.props`.

---

## Refit Contract

```csharp
public interface I{ServiceName}Api
{
    [Post("/v1/charges")]
    Task<CreateChargeResponse> CreateChargeAsync([Body] CreateChargeRequest request, CancellationToken ct = default);

    [Get("/v1/charges/{chargeId}")]
    Task<ChargeResponse> GetChargeAsync(string chargeId, CancellationToken ct = default);

    [Post("/v1/refunds")]
    Task<RefundResponse> CreateRefundAsync([Body] CreateRefundRequest request, CancellationToken ct = default);
}
```

Keep Refit models provider-shaped (snake_case, provider fields), not domain-shaped.

---

## Settings + Auth Handler

```csharp
public class {ServiceName}Settings
{
    public const string ConfigSectionName = "{ServiceName}";
    public string BaseUrl { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public int RetryCount { get; set; } = 3;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 30;
}

public class {ServiceName}AuthHandler(IOptions<{ServiceName}Settings> settings) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new("Bearer", settings.Value.ApiKey);
        return base.SendAsync(request, ct);
    }
}
```

---

## DI + Refit + Resilience

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection Add{ServiceName}(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection({ServiceName}Settings.ConfigSectionName);
        services.Configure<{ServiceName}Settings>(section);
        var settings = section.Get<{ServiceName}Settings>()!;

        services.AddTransient<{ServiceName}AuthHandler>();

        services
            .AddRefitClient<I{ServiceName}Api>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(settings.BaseUrl);
                c.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            })
            .AddHttpMessageHandler<{ServiceName}AuthHandler>()
            .AddResilienceHandler("{ServiceName}", (builder, context) =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = settings.RetryCount,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                });

                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = settings.CircuitBreakerThreshold,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15)
                });

                builder.AddTimeout(TimeSpan.FromSeconds(10));
            });

        services.AddScoped<I{ServiceName}Service, {ServiceName}Service>();
        return services;
    }
}
```

---

## Wrapper Service Pattern

```csharp
public class {ServiceName}Service(
    I{ServiceName}Api api,
    ILogger<{ServiceName}Service> logger) : I{ServiceName}Service
{
    public async Task<DomainResult<PaymentResultDto>> ProcessPaymentAsync(PaymentRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var response = await api.CreateChargeAsync(new CreateChargeRequest
            {
                AmountInCents = request.AmountInCents,
                Currency = request.Currency,
                PaymentSourceToken = request.SourceToken
            }, ct);

            return DomainResult<PaymentResultDto>.Success(new PaymentResultDto
            {
                ExternalId = response.Id,
                Status = MapStatus(response.Status),
                AmountInCents = response.AmountInCents
            });
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "External API failure");
            return DomainResult<PaymentResultDto>.Failure("External provider request failed.");
        }
    }
}
```

Rules:
1. Map provider models to app DTOs in wrapper service.
2. Catch `ApiException`; log detail, return sanitized failure message.
3. Never expose provider internals directly to callers.

---

## Bootstrapper

```csharp
services.Add{ServiceName}(config);
```

---

## Testing

- Unit-test wrapper by mocking `I{ServiceName}Api`.
- Integration tests replace Refit registration with mock service client.
- Validate mapping, retries/fallback behavior, and sanitized error handling.

---

## Verification

- [ ] `Infrastructure.{ServiceName}` contains contract, handler, settings, wrapper, DI extension
- [ ] Refit + resilience packages declared centrally
- [ ] Per-client resilience configured (retry + breaker + timeout)
- [ ] Wrapper implements `Application.Contracts` abstraction
- [ ] No raw `HttpClient` usage in app/domain layers
