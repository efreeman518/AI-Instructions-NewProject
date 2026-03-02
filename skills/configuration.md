# Configuration & Secrets Management

Reference implementation: `sample-app/src/TaskFlow/TaskFlow.Api/appsettings*.json`, `sample-app/src/TaskFlow/TaskFlow.Gateway/appsettings*.json`.

## Prerequisites

- [solution-structure.md](solution-structure.md)
- [aspire.md](aspire.md)
- [iac.md](iac.md)

## Configuration Hierarchy

### Local
1. `appsettings.json`
2. `appsettings.Development.json`
3. User Secrets
4. Environment variables
5. Aspire `WithReference()` injection (highest)

### Azure
1. `appsettings.json` / `appsettings.Production.json`
2. Azure App Configuration
3. Azure Key Vault
4. Container Apps env vars/secrets (highest)

Rule: secrets are never committed. Use User Secrets locally and Key Vault/managed identity in Azure.

---

## Local Setup

### Base appsettings

```json
{
  "AppName": "{Project}Api",
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

### Development overrides

```json
{
  "ConnectionStrings": {
    "{Project}DbContextTrxn": "Server=localhost;Database={Project}Db;Trusted_Connection=true;TrustServerCertificate=true",
    "{Project}DbContextQuery": "Server=localhost;Database={Project}Db;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

### User Secrets

```powershell
dotnet user-secrets init --project src/{Host}/{Host}.Api
dotnet user-secrets set "ServiceAuth:api-cluster:ClientSecret" "your-client-secret" --project src/{Host}/{Host}.Api
dotnet user-secrets set "ConnectionStrings:Redis1" "localhost:6379" --project src/{Host}/{Host}.Api
```

### Aspire override

```csharp
api.WithReference(projectDb, connectionName: "{Project}DbContextTrxn")
   .WithReference(projectDb, connectionName: "{Project}DbContextQuery")
   .WithReference(redis, connectionName: "Redis1");
```

---

## Azure Setup

### Azure App Configuration

```csharp
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(new Uri(config["AzureAppConfigEndpoint"]), credential)
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

### Key Vault direct load

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{vaultName}.vault.azure.net/"),
    credential);
```

### Container Apps env mapping

```bicep
env: [
  { name: 'ConnectionStrings__{Project}DbContextTrxn', secretRef: 'sql-trxn-connstr' }
  { name: 'ConnectionStrings__Redis1', secretRef: 'redis-connstr' }
  { name: 'AzureAppConfigEndpoint', value: appConfig.outputs.endpoint }
]
```

`__` maps to `:` in .NET config keys.

---

## Options & Feature Flags

```json
{
  "FeatureFlags": {
    "EnableNotifications": true,
    "EnableScheduler": true
  }
}
```

```csharp
public class NotificationOptions
{
    public const string SectionName = "Notifications";
    public bool EnableEmail { get; set; }
    public bool EnableSms { get; set; }
    public string SendGridApiKey { get; set; } = string.Empty;
}

services.Configure<NotificationOptions>(config.GetSection(NotificationOptions.SectionName));
```

---

## Local Stub Pattern (Required)

Register stubs when credentials are missing so the app builds/runs without external secrets.

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication("DevBypass")
        .AddScheme<AuthenticationSchemeOptions, DevBypassAuthHandler>("DevBypass", null);
}

if (string.IsNullOrEmpty(config["Stripe:ApiKey"]))
    services.AddSingleton<IStripeService, StubStripeService>();
else
    services.AddScoped<IStripeService, StripeService>();
```

---

## Rules

1. Never commit secrets.
2. Prefer managed identity over secrets.
3. Keep config keys consistent across API/Gateway/Scheduler/Functions/UI.
4. Use `Sentinel` + labels for App Configuration refresh strategy.
5. In lite mode, keep to `appsettings` + User Secrets and add App Config/Key Vault later.
6. Map compliance metadata from `resource-implementation.yaml` to configuration controls (classification labels, retention switches, audit flags).

## Verification

- [ ] No real secrets in committed appsettings files
- [ ] Aspire connection names match appsettings keys
- [ ] User Secrets initialized for host projects
- [ ] App Config label selection matches environment
- [ ] Key Vault resolves via managed identity
- [ ] `ASPNETCORE_ENVIRONMENT` is correctly set per target
