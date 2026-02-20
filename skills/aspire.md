# Aspire Orchestration

## Overview

.NET Aspire provides **local development orchestration** — wiring up SQL Server, Redis, APIs, and other services via an AppHost project. It handles service discovery, health checks, telemetry (OpenTelemetry), and can provision dev tunnels for webhook testing.

## Project Structure

> **Reference implementation:** See `sampleapp/src/Aspire/AppHost/` and `sampleapp/src/Aspire/ServiceDefaults/`

```
Aspire/
├── AppHost/
│   ├── AppHost.cs              # Orchestration definition
│   ├── AppHost.csproj
│   ├── appsettings.json
│   └── Properties/launchSettings.json
└── ServiceDefaults/
    ├── Extensions.cs           # Shared telemetry, resilience, health
    └── ServiceDefaults.csproj
```

## AppHost Pattern

> **Reference implementation:** See `sampleapp/src/Aspire/AppHost/AppHost.cs` for the full orchestration setup with SQL Server, Redis, API, Scheduler, Gateway, and dev tunnels.

### Aspire Integration & Hosting Package Discovery

When the project requires infrastructure services (SQL Server, Redis, Cosmos DB, Azure Storage, Key Vault, Service Bus, etc.), **always search for the official Aspire hosting/integration NuGet packages first:**

1. **Search NuGet** for packages matching `owner:Aspire tags:integration+hosting` (or search `Aspire.Hosting.{ServiceName}`).
2. **Aspire.Hosting has many ready-to-use packages** — each with a README describing usage:
   - `Aspire.Hosting.Azure.Sql` / `Aspire.Hosting.SqlServer` — SQL Server
   - `Aspire.Hosting.Redis` — Redis cache
   - `Aspire.Hosting.Azure.CosmosDB` — Cosmos DB
   - `Aspire.Hosting.Azure.Storage` — Azure Blob/Table/Queue Storage
   - `Aspire.Hosting.Azure.KeyVault` — Azure Key Vault
   - `Aspire.Hosting.Azure.ServiceBus` — Azure Service Bus
   - `Aspire.Hosting.Azure.EventHubs` — Azure Event Hubs
   - `Aspire.Hosting.MongoDB` / `Aspire.Hosting.PostgreSQL` / `Aspire.Hosting.RabbitMQ` — other common services
3. If no official Aspire hosting package exists for the service, **look for an emulator** (e.g., Azurite for Storage, CosmosDB emulator) and wire it as a container resource.
4. If neither an Aspire package nor emulator is available, use a Docker container resource (`builder.AddContainer(...)`) or skip the dependency with a stub/mock.
5. Always check the package README for correct API usage — Aspire APIs evolve across versions.

> **After adding any Aspire hosting packages to `AppHost.csproj` or integration packages to service projects, update `Directory.Packages.props` to the latest stable versions and run `dotnet restore`.**

```csharp
// Compact pattern — see sampleapp for full implementation
var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sql", password, port: 38433)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("{project}-sql-data");
var projectDb = sqlServer.AddDatabase("{project}db");
var redis = builder.AddRedis("redis");

var api = builder.AddProject<Projects.{Host}_Api>("{host}api")
    .WithReference(projectDb, connectionName: "{Project}DbContextTrxn")
    .WithReference(redis, connectionName: "Redis1");

var gateway = builder.AddProject<Projects.{Gateway}_Gateway>("{gateway}")
    .WithReference(api)
    .WaitFor(api);  // Don't start until API is healthy

await builder.Build().RunAsync();
```

## Service Defaults

Shared project referenced by all deployable projects for consistent telemetry and resilience:

> **Reference implementation:** See `sampleapp/src/Aspire/ServiceDefaults/Extensions.cs`

```csharp
// Compact pattern — see sampleapp for full implementation
public static IHostApplicationBuilder AddServiceDefaults(
    this IHostApplicationBuilder builder, IConfiguration config, string appName)
{
    builder.ConfigureOpenTelemetry();
    builder.AddDefaultHealthChecks();
    builder.Services.AddServiceDiscovery();
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();
        http.AddServiceDiscovery();
    });
    return builder;
}
```

## Key Concepts

### Service Discovery

With Aspire, downstream service URLs are resolved automatically. In YARP config, cluster destinations use Aspire service names:

```json
{
  "Clusters": {
    "api-cluster": {
      "Destinations": {
        "api": { "Address": "https+http://{host}api" }
      }
    }
  }
}
```

YARP's `.AddServiceDiscoveryDestinationResolver()` resolves these at runtime.

### Connection String Injection

```csharp
.WithReference(projectDb, connectionName: "{Project}DbContextTrxn")
```

This injects the connection string into the project's configuration under `ConnectionStrings:{Project}DbContextTrxn`, matching what the Bootstrapper expects.

### Persistent Volumes

```csharp
.WithDataVolume("{project}-sql-data")
```

SQL data persists across container restarts. To reset: `docker volume rm {project}-sql-data`.

### Dev Tunnels

For testing external webhooks during local development. Dev tunnels create a publicly accessible URL that routes to your local Gateway.

**Key pattern:** Use a **persisted tunnel ID** so the tunnel URL stays the same across restarts.

> **Reference implementation:** See `sampleapp/src/Aspire/AppHost/AppHost.cs` for the full dev tunnel configuration.

```csharp
// Compact pattern — persisted tunnel ID
var tunnel = builder.AddDevTunnel(
    name: "{gateway}-tunnel",
    tunnelId: "{project}-dev-tunnel",    // Persisted ID — same URL every time
    options: new DevTunnelOptions { AllowAnonymous = false });
tunnel.WithReference(gateway.GetEndpoint("https-gateway"), allowAnonymous: true);
```

**Important:**
- The `tunnelId` parameter persists the tunnel across restarts — the external URL remains stable
- Set `AllowAnonymous = false` at the tunnel level for security
- Use `allowAnonymous: true` selectively on specific endpoint references that external services call back to
- Dev tunnels are **local development only** — they are not deployed to Azure (see the mapping table in [iac.md](iac.md))

**Aspire AppHost.csproj** must include:
```xml
<PackageReference Include="Aspire.Hosting.DevTunnels" />
```

### WaitFor

```csharp
gateway.WaitFor(api);
```

Gateway won't start until API passes health checks.

## AppHost.csproj

> **Reference implementation:** See `sampleapp/src/Aspire/AppHost/AppHost.csproj`

Versions and `TargetFramework` below are example values. Use the latest stable versions that match your installed SDK/tooling.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="10.0.x" />
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsAspireHost>true</IsAspireHost>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" />
    <PackageReference Include="Aspire.Hosting.SqlServer" />
    <PackageReference Include="Aspire.Hosting.Redis" />
    <PackageReference Include="Aspire.Hosting.DevTunnels" />
  </ItemGroup>
</Project>
```

## Running with Aspire

```bash
# From src/Aspire/AppHost
dotnet run
```

Opens the Aspire dashboard at `https://localhost:15888` showing all resources, logs, traces, and metrics.

## AppHost CLI Prerequisites (Important)

When running the AppHost from the CLI (outside an IDE launch profile), several prerequisites must be satisfied or the host will fail at startup.

### Sdk Stanza

The AppHost `.csproj` **must** include the Aspire AppHost SDK import. Without it, dashboard and orchestration metadata are missing and startup fails immediately:

```xml
<Sdk Name="Aspire.AppHost.Sdk" Version="10.0.x" />
```

If the AppHost fails with errors about missing dashboard paths or orchestration metadata, verify this stanza first.

### Required Environment Variables for CLI Runs

When running `dotnet run` from a terminal (not via an IDE launch profile), the Aspire dashboard expects these environment variables:

| Variable | Purpose |
|----------|---------|
| `ASPNETCORE_URLS` | Listen URL for the AppHost process (e.g., `http://localhost:15888`) |
| `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` | gRPC OTLP endpoint for dashboard telemetry |
| `ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL` | HTTP OTLP endpoint (alternative to gRPC) |
| `ASPIRE_ALLOW_UNSECURED_TRANSPORT` | Set to `true` when using HTTP-only local endpoints |

> **Note:** IDE launch profiles (e.g., `launchSettings.json` for Visual Studio or VS Code) typically set these automatically. CLI runs need them explicitly.

### PowerShell Example — CLI Run with Env Vars

```powershell
# Set required env vars for local HTTP dashboard
$env:ASPNETCORE_URLS = "http://localhost:15888"
$env:ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL = "http://localhost:18889"
$env:ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL = "http://localhost:18890"
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"

# Run AppHost
dotnet run --project src/Aspire/AppHost
```

## Common AppHost Startup Errors

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Missing `CliPath` or `DashboardPath` at startup | `Aspire.AppHost.Sdk` stanza missing from `.csproj` | Add `<Sdk Name="Aspire.AppHost.Sdk" Version="10.0.x" />` to AppHost.csproj |
| `ASPNETCORE_URLS` not configured / bind failure | Env var not set for CLI run | Set `$env:ASPNETCORE_URLS` before `dotnet run` |
| OTLP endpoint unavailable / dashboard shows no telemetry | `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` not set | Set OTLP endpoint env vars (gRPC and/or HTTP) |
| HTTPS required / certificate errors in local dev | Unsecured transport not allowed by default | Set `$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"` for local HTTP |
| Dashboard opens but shows no resources | AppHost build succeeded but orchestration metadata missing | Rebuild AppHost; confirm Sdk stanza and all `AddProject` references |

## Local Cohesion Check (before Azure IaC)

After `dotnet run` in AppHost, verify these before moving to Bicep/deployment work:

1. API healthy and reachable from Gateway
2. Gateway starts only after API health (`WaitFor(api)`)
3. Scheduler runs with single replica and receives `SchedulerDbContext`
4. SQL + Redis references match the connection names expected by Bootstrapper
5. If Functions are enabled, decide whether the project is still template-stubbed or promoted to `skills/function-app.md` starter/full profile

If any item fails locally in Aspire, fix that first; do not compensate in IaC.

---

## Verification

After generating the Aspire AppHost, confirm:

- [ ] `AppHost.csproj` references all deployable projects
- [ ] SQL Server uses a data volume for persistence across restarts
- [ ] Redis is added with `AddRedis` (not a connection string)
- [ ] Gateway `WaitFor(api)` and `WaitFor(scheduler)` are present
- [ ] Scheduler replica count is set to `1`
- [ ] All `WithReference` calls match connection names expected by Bootstrapper
- [ ] Dev tunnel uses a persisted `tunnelId` (if webhooks are needed)
- [ ] Function App (if included) uses `AddAzureFunctionsProject` or is stubbed with a comment
- [ ] `dotnet run` from AppHost starts all resources and Aspire dashboard opens
- [ ] Cross-references: [iac.md](iac.md) mapping table has a row for every Aspire resource
