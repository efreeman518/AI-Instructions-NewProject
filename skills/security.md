# Security

## Purpose

Hardening checklist for API, Gateway, and optional hosts. Complements [identity-management.md](identity-management.md) (which handles authn); this covers authz patterns, transport security, and input safety.

---

## Rate Limiting

Use ASP.NET `RateLimiterMiddleware` for request throttling.

### Patterns

```csharp
// In RegisterApiServices.cs
services.AddRateLimiter(options =>
{
    // Fixed window per-tenant
    options.AddPolicy("PerTenant", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User?.FindFirst("tenant_id")?.Value ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Sliding window per-endpoint
    options.AddPolicy("PerEndpoint", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Request.Path.Value ?? "/",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromSeconds(30),
                SegmentsPerWindow = 3
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

### Pipeline Registration

```csharp
app.UseRateLimiter();  // After UseRouting, before UseAuthorization
```

### Testing

> **CRITICAL:** Disable rate limiter in `CustomApiFactory` for integration tests to avoid flaky 429 responses:
> ```csharp
> services.Configure<RateLimiterOptions>(o => o.GlobalLimiter = PartitionedRateLimiter.CreateChained<HttpContext>());
> ```

---

## Input Validation & Sanitization

### DTO Structure Validation

Use [structure-validator-template](../templates/structure-validator-template.md) for DTO shape validation before domain operations. Validates required fields, string lengths, enum ranges.

### String Safety

- HTML-encode user-generated content before storage if it will be rendered in UI: `System.Net.WebUtility.HtmlEncode(input)`.
- Enforce `MaxLength` at both DTO (StructureValidator) and EF (`HasMaxLength()`) levels — defense in depth.
- Reject null bytes and control characters in text inputs.

---

## Security Headers

Add middleware to set security headers on all responses:

```csharp
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // HSTS — set via config toggle, not in middleware (UseHsts in pipeline)
        // Content-Security-Policy — set in Gateway for UI responses only

        await next(context);
    }
}
```

Register early in pipeline — before routing.

For UI hosts (Gateway serving Uno WASM), add `Content-Security-Policy` with appropriate directives. Use config-driven toggle to adjust between dev/prod.

---

## CORS Policy

CORS configuration belongs in the **Gateway only**. API behind gateway should reject direct browser requests.

### Configuration Pattern

```json
// appsettings.json
{
  "CorsSettings": {
    "AllowedOrigins": ["https://localhost:5001", "https://myapp.azurewebsites.net"],
    "AllowCredentials": true
  }
}
```

```csharp
// Gateway RegisterServices
services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = config.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
```

---

## Dependency Scanning

### CI Pipeline

Add `dotnet nuget audit` to CI builds:

```yaml
- script: dotnet restore --locked-mode
- script: dotnet nuget audit --level moderate --output json
```

### GitHub Dependabot

```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
    open-pull-requests-limit: 10
```

### NuGet Vulnerability Alerts

Enable in `Directory.Build.props` or `.csproj`:
```xml
<PropertyGroup>
  <NuGetAudit>true</NuGetAudit>
  <NuGetAuditLevel>moderate</NuGetAuditLevel>
</PropertyGroup>
```

---

## Secret Rotation

Secrets must be stored in Azure Key Vault (see [skills/keyvault.md](keyvault.md)).

### Rotation Workflow

1. **Create new version** — add new secret version in Key Vault
2. **Update config** — deploy app with config pointing to latest version (Key Vault references auto-resolve latest)
3. **Verify** — confirm app functions with new secret
4. **Remove old version** — disable/remove previous version after grace period

### Configuration Validation

> **CRITICAL:** Use `ValidateOnStart()` for options that bind to secrets — fail fast if secrets are missing or expired rather than failing on first request:
> ```csharp
> services.AddOptions<DatabaseSettings>()
>     .BindConfiguration("DatabaseSettings")
>     .ValidateDataAnnotations()
>     .ValidateOnStart();
> ```

---

## Verification Checklist

- [ ] Rate limiting registered with per-tenant and/or per-endpoint policies
- [ ] Rate limiter disabled in `CustomApiFactory` for tests
- [ ] `StructureValidator` enforces `MaxLength` matching EF configuration
- [ ] Security headers middleware added (X-Content-Type-Options, X-Frame-Options)
- [ ] CORS configured in Gateway only — API rejects direct browser requests
- [ ] `dotnet nuget audit` included in CI pipeline
- [ ] Dependabot configured for NuGet ecosystem
- [ ] Secrets stored in Key Vault with rotation workflow documented
- [ ] `ValidateOnStart()` used for critical configuration sections
