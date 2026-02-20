# Configuration & Secrets Management

## Prerequisites

- [solution-structure.md](solution-structure.md) — project layout and appsettings conventions
- [aspire.md](aspire.md) — Aspire orchestration and connection string injection
- [iac.md](iac.md) — Azure resource provisioning

## Overview

Configuration flows through a **layered hierarchy** that progresses from local development to cloud deployment. Each layer can override the previous, allowing environment-specific values without code changes.

---

## Configuration Hierarchy

### Local Development

```
1. appsettings.json                    ← Base defaults (committed to repo)
2. appsettings.Development.json        ← Dev-specific overrides (committed)
3. User Secrets (secrets.json)         ← Developer-local secrets (NOT committed)
4. Environment Variables               ← Set by IDE or shell
5. Aspire AppHost injection            ← Connection strings, service URLs (overrides everything)
```

### Azure (Deployed)

```
1. appsettings.json                    ← Baked into container image
2. appsettings.Production.json         ← Baked into container image
3. Azure App Configuration             ← Centralized, dynamic (Key Vault references for secrets)
4. Azure Key Vault                     ← Secrets (connection strings, API keys, certificates)
5. Container Apps env vars / secrets   ← Set via Bicep or CLI (highest priority)
```

> **Key principle:** Secrets never go in `appsettings.json` or source control. In local dev, use User Secrets or Aspire injection. In Azure, use Key Vault (accessed via managed identity).

---

## Local Development Setup

### appsettings.json (API)

Base settings committed to the repo. Contains structure and non-sensitive defaults:

```json
{
  "AppName": "{Project}Api",
  "Logging": {
    "LogLevel": { "Default": "Information" }
  },
  "ConnectionStrings": {
    "{Project}DbContextTrxn": "",
    "{Project}DbContextQuery": "",
    "Redis1": ""
  },
  "FeatureFlags": {
    "EnableNotifications": false
  }
}
```

### appsettings.Development.json

Dev-specific overrides (safe to commit — no real secrets):

```json
{
  "Logging": {
    "LogLevel": { "Default": "Debug" }
  },
  "ConnectionStrings": {
    "{Project}DbContextTrxn": "Server=localhost;Database={Project}Db;Trusted_Connection=true;TrustServerCertificate=true",
    "{Project}DbContextQuery": "Server=localhost;Database={Project}Db;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

### User Secrets

For local-only secrets that must not be committed:

```bash
# Initialize user secrets for a project
dotnet user-secrets init --project src/{Host}/{Host}.Api

# Set secrets
dotnet user-secrets set "ServiceAuth:api-cluster:ClientSecret" "your-client-secret" --project src/{Host}/{Host}.Api
dotnet user-secrets set "ConnectionStrings:Redis1" "localhost:6379" --project src/{Host}/{Host}.Api
```

User secrets are stored at:
- **Windows:** `%APPDATA%\Microsoft\UserSecrets\{guid}\secrets.json`
- **macOS/Linux:** `~/.microsoft/usersecrets/{guid}/secrets.json`

### Aspire Injection (Overrides Everything Locally)

When running via Aspire AppHost, connection strings and service URLs are injected automatically:

```csharp
// In AppHost — these override appsettings.json values
api.WithReference(projectDb, connectionName: "{Project}DbContextTrxn")
   .WithReference(projectDb, connectionName: "{Project}DbContextQuery")
   .WithReference(redis, connectionName: "Redis1");
```

> **Aspire always wins locally.** Even if `appsettings.Development.json` has a connection string, the Aspire-injected value takes precedence because environment variables are higher priority than JSON config.

---

## Azure Configuration

### Azure App Configuration

Centralized configuration store for all environments. Supports:
- Key-value pairs (plain settings)
- Key Vault references (secrets)
- Feature flags
- Labels for per-environment filtering

```csharp
// In Program.cs — load Azure App Configuration
builder.Configuration.AddAzureAppConfiguration(options =>
{
    var endpoint = config["AzureAppConfigEndpoint"];
    options.Connect(new Uri(endpoint), credential)
        .Select(KeyFilter.Any, LabelFilter.Null)
        .Select(KeyFilter.Any, builder.Environment.EnvironmentName)
        .ConfigureKeyVault(kv => kv.SetCredential(credential))
        .ConfigureRefresh(refresh =>
        {
            refresh.Register("Sentinel", refreshAll: true)
                   .SetRefreshInterval(TimeSpan.FromMinutes(5));
        });
});
```

### Azure Key Vault

Secrets accessed via managed identity:

```csharp
// Direct Key Vault integration (alternative to App Config references)
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{vaultName}.vault.azure.net/"),
    credential);
```

### Container Apps Secrets

Secrets injected as environment variables via Bicep:

```bicep
env: [
  { name: 'ConnectionStrings__{Project}DbContextTrxn', secretRef: 'sql-trxn-connstr' }
  { name: 'ConnectionStrings__Redis1', secretRef: 'redis-connstr' }
  { name: 'AzureAppConfigEndpoint', value: appConfig.outputs.endpoint }
]
```

> **Double underscore `__`** maps to `:` in .NET configuration. `ConnectionStrings__Redis1` → `ConnectionStrings:Redis1`.

---

## Configuration Patterns

### Feature Flags

```json
{
  "FeatureFlags": {
    "EnableNotifications": true,
    "EnableScheduler": true,
    "EnableNewDashboard": false
  }
}
```

```csharp
// Reading feature flags
var enableNotifications = config.GetValue<bool>("FeatureFlags:EnableNotifications");
```

For more advanced scenarios, use Azure App Configuration feature management:

```csharp
builder.Services.AddAzureAppConfiguration()
    .AddFeatureManagement();
```

### Strongly-Typed Options

Bind configuration sections to typed classes:

```csharp
// Options class
public class NotificationOptions
{
    public const string SectionName = "Notifications";
    public bool EnableEmail { get; set; }
    public bool EnableSms { get; set; }
    public string SendGridApiKey { get; set; } = string.Empty;
}

// Registration in Bootstrapper
services.Configure<NotificationOptions>(config.GetSection(NotificationOptions.SectionName));

// Usage via DI
public class NotificationService(IOptions<NotificationOptions> options)
{
    private readonly NotificationOptions _opts = options.Value;
}
```

### Per-Environment Overrides

```
appsettings.json                 → base (all environments)
appsettings.Development.json     → local dev
appsettings.Staging.json         → staging (optional, usually use App Config)
appsettings.Production.json      → production (optional, usually use App Config)
```

Set the environment:
- **Local:** `ASPNETCORE_ENVIRONMENT=Development` (default)
- **Container Apps:** Set via env var in Bicep: `{ name: 'ASPNETCORE_ENVIRONMENT', value: envName }`

---

## Secret Rotation Strategy

| Secret | Location | Rotation |
|--------|----------|----------|
| SQL connection string | Key Vault → App Config reference | Rotate in Key Vault; App Config auto-refreshes |
| Redis connection string | Key Vault → Container Apps secret | Rotate in Key Vault; redeploy or restart container |
| Entra client secret | Key Vault | Rotate via Azure CLI / Portal; use certificate auth for zero-downtime |
| SendGrid / Twilio API keys | Key Vault | Rotate in Key Vault; App Config reference refreshes |
| Managed identity | Azure RBAC | No rotation needed — Azure handles token lifecycle |

> **Prefer managed identity** wherever possible (SQL, Key Vault, App Config, ACR). This eliminates secret rotation entirely for those resources.

---

## Configuration by Host

| Host | Config Sources | Notes |
|------|---------------|-------|
| **API** | `appsettings.json` + User Secrets + Aspire + App Config + Key Vault | Full config hierarchy |
| **Gateway** | `appsettings.json` + User Secrets + Aspire | YARP routes, auth settings, token service config |
| **Scheduler** | `appsettings.json` + User Secrets + Aspire + App Config | TickerQ settings, dashboard credentials |
| **Function App** | `local.settings.json` (local) + App Settings (Azure) | Functions use `local.settings.json` instead of `appsettings.json` locally |
| **Uno UI** | `appsettings.json` embedded in assets | Client-side config (gateway URL, auth endpoints) |

---

## Lite Mode Considerations

In **Lite mode** (`scaffoldMode: lite`):

- Use `appsettings.json` + User Secrets only (no Azure App Configuration)
- No Key Vault integration (secrets in User Secrets locally, Container Apps env vars in Azure)
- Single connection string (`{Project}DbContext`) instead of split Trxn/Query
- No Redis connection string (caching disabled)
- Add Azure App Configuration + Key Vault when graduating to full mode

## Stubbing External Services for Local Compilation

External services that require configuration (authentication providers, third-party APIs, messaging infrastructure, etc.) often block local compilation if their SDKs fail at startup without valid credentials. **Always stub or mock these services so the project compiles and runs locally without live credentials.**

### Strategy

1. **Authentication providers** (Entra ID, Entra External, OAuth): Register a no-op or stub middleware that bypasses real token validation in `Development` environment. The Bootstrapper should detect `ASPNETCORE_ENVIRONMENT=Development` and conditionally register a mock auth handler:
   ```csharp
   if (builder.Environment.IsDevelopment())
   {
       builder.Services.AddAuthentication("DevBypass")
           .AddScheme<AuthenticationSchemeOptions, DevBypassAuthHandler>("DevBypass", null);
   }
   ```
2. **External API clients** (Stripe, SendGrid, Twilio, etc.): Register a mock/stub implementation of the service interface when the API key is not configured. Use a pattern like:
   ```csharp
   if (string.IsNullOrEmpty(config["Stripe:ApiKey"]))
       services.AddSingleton<IStripeService, StubStripeService>();
   else
       services.AddScoped<IStripeService, StripeService>();
   ```
3. **Messaging** (Service Bus, Event Grid): If no connection string is configured, register a no-op sender/publisher that logs the message instead of sending it.
4. **Key Vault**: If `VaultUri` is not configured, skip Key Vault registration and rely on User Secrets / appsettings for secrets locally.

### Rules

- **The project must always compile and start** with only `appsettings.Development.json` values — no live external credentials required for local dev.
- Stubs should log a warning at startup: `"Using stub {ServiceName} — configure credentials for real integration."`
- Stubs must implement the same interface as the real service so DI wiring is unchanged.
- Add `// STUB: Replace with real implementation when credentials are available` comments in stub classes.
- The Aspire AppHost should NOT require external service credentials to start — only local infrastructure (SQL, Redis) via containers.

---

## Rules

1. **Never commit secrets** — API keys, connection strings with passwords, and client secrets must be in User Secrets (local) or Key Vault (Azure). Never in `appsettings.json` or source control.
2. **Aspire overrides everything locally** — connection strings from `WithReference()` take precedence over `appsettings.Development.json`.
3. **Use managed identity** — for SQL, Key Vault, App Config, and ACR. Eliminates secret rotation.
4. **Double underscore for nesting** — `ConnectionStrings__{Project}DbContextTrxn` maps to `ConnectionStrings:{Project}DbContextTrxn` in .NET config.
5. **Sentinel key for refresh** — use a `Sentinel` key in Azure App Configuration to trigger full config refresh.
6. **Label-based filtering** — use labels in App Config to serve different values per environment without separate stores.
7. **Feature flags** — simple flags go in `appsettings.json`. Advanced targeting and rollout use Azure App Configuration feature management.
8. **Placeholder tokens** — see [placeholder-tokens.md](../placeholder-tokens.md) for all token definitions.

---

## Verification

After configuring secrets and settings, verify:

- [ ] `appsettings.json` contains no real secrets (connection strings are empty or point to localhost)
- [ ] `appsettings.Development.json` overrides work correctly when running outside Aspire
- [ ] User secrets are initialized for each host project (`dotnet user-secrets list --project ...`)
- [ ] Aspire AppHost injects connection strings that match the keys in `appsettings.json`
- [ ] All `ConnectionStrings__*` env vars use double underscore notation
- [ ] Azure App Configuration loads correctly with label filtering by environment name
- [ ] Key Vault references in App Configuration resolve successfully via managed identity
- [ ] Feature flags toggle behavior correctly in dev and production
- [ ] `ASPNETCORE_ENVIRONMENT` is set correctly for each deployment target
