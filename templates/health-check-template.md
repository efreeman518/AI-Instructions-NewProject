# Health Check Template

**Generates:** `SqlHealthCheck.cs`, `RedisHealthCheck.cs` (and per-dependency checks as needed)
**Requires:** [../skills/observability.md](../skills/observability.md)

## Health Check Implementation

```csharp
public class SqlHealthCheck(IDbContextFactory<{App}DbContextTrxn> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var db = await factory.CreateDbContextAsync(ct);
            await db.Database.CanConnectAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL connection failed", ex);
        }
    }
}
```

## Registration

```csharp
// In RegisterApiServices or Bootstrapper
services.AddHealthChecks()
    .AddCheck<SqlHealthCheck>("sql", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
```

## Endpoint Mapping

```csharp
app.MapHealthChecks("/healthz", new() { Predicate = _ => true });           // liveness
app.MapHealthChecks("/readyz", new() { Predicate = r => r.Tags.Contains("ready") }); // readiness
```

## Rules

- One `IHealthCheck` class per external dependency.
- Tag readiness checks with `"ready"` so liveness (`/healthz`) includes all, readiness (`/readyz`) filters.
- Do not duplicate ServiceDefaults basic liveness checks — add domain-specific readiness only.
