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

When the project requires infrastructure services (databases, caches, messaging, Azure services, etc.), **always search for the official Aspire hosting/integration NuGet packages first.** The `Aspire.Hosting.*` family provides ready-to-use local container orchestration for dozens of services — no manual Docker Compose needed.

#### Discovery Steps

1. **Search NuGet** for packages matching `owner:Aspire tags:integration+hosting` (or search `Aspire.Hosting.{ServiceName}`).
2. **Install the hosting package** in `AppHost.csproj` and the corresponding **integration/client package** in the consuming service project.
3. If the service is an Azure resource, check if the hosting package supports **`.RunAsEmulator()`** — this spins up a local emulator container automatically (see table below).
4. If no official Aspire hosting package exists, **look for a standalone emulator** (e.g., install Azurite via npm, run Cosmos DB Emulator locally) and wire it as a container resource.
5. If neither an Aspire package nor emulator is available, use a Docker container resource (`builder.AddContainer(...)`) or skip the dependency with a stub/mock.
6. Always check the package README for correct API usage — Aspire APIs evolve across versions.

> **After adding any Aspire hosting packages to `AppHost.csproj` or integration packages to service projects, update `Directory.Packages.props` to the latest stable versions and run `dotnet restore`.**

#### Comprehensive Aspire.Hosting.* Package Catalog

Below is the full catalog organized by category. Use this as a lookup whenever adding an infrastructure dependency. *(NuGet `owner:aspire` — always verify the latest version.)*

**Databases — Relational**

| Package | Local Container | Builder API | Notes |
|---------|----------------|-------------|-------|
| `Aspire.Hosting.SqlServer` | SQL Server (Docker) | `builder.AddSqlServer(...)` | Use `.WithDataVolume()` for persistence |
| `Aspire.Hosting.Azure.Sql` | Azure SQL Edge container | `builder.AddAzureSqlServer(...)` | Azure-provisioned in deploy; local container in dev |
| `Aspire.Hosting.PostgreSQL` | PostgreSQL (Docker) | `builder.AddPostgres(...)` | Supports `.WithPgAdmin()` for local admin UI |
| `Aspire.Hosting.MySql` | MySQL (Docker) | `builder.AddMySql(...)` | Supports `.WithPhpMyAdmin()` |
| `Aspire.Hosting.Oracle` | Oracle Database Free (Docker) | `builder.AddOracleDatabaseServer(...)` | Large image — first pull is slow |

**Databases — NoSQL & Document**

| Package | Local Container | Builder API | Emulator Support |
|---------|----------------|-------------|-----------------|
| `Aspire.Hosting.Azure.CosmosDB` | Cosmos DB Emulator (Docker) | `builder.AddAzureCosmosDB(...).RunAsEmulator()` | **Yes** — `.RunAsEmulator()` starts the emulator container automatically |
| `Aspire.Hosting.MongoDB` | MongoDB (Docker) | `builder.AddMongoDB(...)` | Supports `.WithMongoExpress()` for local admin UI |

**Caching**

| Package | Local Container | Builder API | Notes |
|---------|----------------|-------------|-------|
| `Aspire.Hosting.Redis` | Redis (Docker) | `builder.AddRedis(...)` | Supports `.WithRedisCommander()` / `.WithRedisInsight()` for local admin |
| `Aspire.Hosting.Azure.Redis` | Redis container locally | `builder.AddAzureRedis(...)` | Azure Cache for Redis in deploy; local Redis container in dev |
| `Aspire.Hosting.Garnet` | Garnet (Docker) | `builder.AddGarnet(...)` | Microsoft's Redis-compatible cache — high performance |
| `Aspire.Hosting.Valkey` | Valkey (Docker) | `builder.AddValkey(...)` | Open-source Redis fork |

**Messaging & Eventing**

| Package | Local Container | Builder API | Emulator Support |
|---------|----------------|-------------|-----------------|
| `Aspire.Hosting.RabbitMQ` | RabbitMQ (Docker) | `builder.AddRabbitMQ(...)` | Includes management UI by default |
| `Aspire.Hosting.Kafka` | Kafka (Docker) | `builder.AddKafka(...)` | Supports `.WithKafkaUI()` |
| `Aspire.Hosting.Nats` | NATS (Docker) | `builder.AddNats(...)` | Lightweight messaging |
| `Aspire.Hosting.Azure.ServiceBus` | Service Bus Emulator (Docker) | `builder.AddAzureServiceBus(...).RunAsEmulator()` | **Yes** — `.RunAsEmulator()` starts the emulator container |
| `Aspire.Hosting.Azure.EventHubs` | Event Hubs Emulator (Docker) | `builder.AddAzureEventHubs(...).RunAsEmulator()` | **Yes** — `.RunAsEmulator()` starts the emulator container |

**Azure Storage**

| Package | Local Container | Builder API | Emulator Support |
|---------|----------------|-------------|-----------------|
| `Aspire.Hosting.Azure.Storage` | Azurite (Docker) | `builder.AddAzureStorage(...).RunAsEmulator()` | **Yes** — `.RunAsEmulator()` starts Azurite for Blob/Table/Queue |

**Search & AI**

| Package | Local Container | Builder API | Emulator Support |
|---------|----------------|-------------|-----------------|
| `Aspire.Hosting.Elasticsearch` | Elasticsearch (Docker) | `builder.AddElasticsearch(...)` | Full local container |
| `Aspire.Hosting.Azure.Search` | — | `builder.AddAzureSearch(...)` | **No local emulator** — use Azure resource or stub |
| `Aspire.Hosting.Milvus` | Milvus (Docker) | `builder.AddMilvus(...)` | Vector DB for AI workloads |
| `Aspire.Hosting.Qdrant` | Qdrant (Docker) | `builder.AddQdrant(...)` | Vector DB for AI workloads |

**Observability & Logging**

| Package | Local Container | Builder API | Notes |
|---------|----------------|-------------|-------|
| `Aspire.Hosting.Seq` | Seq (Docker) | `builder.AddSeq(...)` | Structured log viewer — great for local dev |
| `Aspire.Hosting.Azure.ApplicationInsights` | — | `builder.AddAzureApplicationInsights(...)` | Cloud-only — use Seq or Aspire dashboard locally |

**Identity & Security**

| Package | Local Container | Builder API | Notes |
|---------|----------------|-------------|-------|
| `Aspire.Hosting.Keycloak` | Keycloak (Docker) | `builder.AddKeycloak(...)` | Full local IdP for auth testing |
| `Aspire.Hosting.Azure.KeyVault` | — | `builder.AddAzureKeyVault(...)` | **No local emulator** — use `appsettings.Development.json` or user-secrets locally |

**Application Hosting**

| Package | Purpose | Builder API | Notes |
|---------|---------|-------------|-------|
| `Aspire.Hosting.JavaScript` | Node.js / npm apps | `builder.AddNpmApp(...)` | Replaces deprecated `Aspire.Hosting.NodeJs` |
| `Aspire.Hosting.Python` | Python apps | `builder.AddPythonApp(...)` | Supports venv activation |

#### The `.RunAsEmulator()` Pattern

> **Note:** All Aspire hosting packages (`Aspire.Hosting.*`) already provide container lifecycle management, connection string injection, port mapping, and dashboard visibility. `.RunAsEmulator()` is **not** what gives you those benefits — every `builder.AddRedis(...)`, `builder.AddSqlServer(...)`, etc. already does that.

`.RunAsEmulator()` applies **only to Azure-branded hosting packages** whose default behavior is to provision a real Azure resource. It tells Aspire to run a local emulator container instead of connecting to Azure during development.

**Services that support `.RunAsEmulator()`:**

| Package | Without `.RunAsEmulator()` | With `.RunAsEmulator()` |
|---------|---------------------------|------------------------|
| `Aspire.Hosting.Azure.CosmosDB` | Provisions real Azure Cosmos DB | Runs Cosmos DB Emulator container locally |
| `Aspire.Hosting.Azure.Storage` | Provisions real Azure Storage Account | Runs Azurite container locally |
| `Aspire.Hosting.Azure.ServiceBus` | Provisions real Azure Service Bus | Runs Service Bus Emulator container locally |
| `Aspire.Hosting.Azure.EventHubs` | Provisions real Azure Event Hubs | Runs Event Hubs Emulator container locally |

**Services that do NOT need `.RunAsEmulator()`** — they already run local containers by default:

| Package | What Happens |
|---------|-------------|
| `Aspire.Hosting.SqlServer` | Runs SQL Server container locally |
| `Aspire.Hosting.Redis` | Runs Redis container locally |
| `Aspire.Hosting.PostgreSQL` | Runs PostgreSQL container locally |
| `Aspire.Hosting.MongoDB` | Runs MongoDB container locally |
| `Aspire.Hosting.RabbitMQ` | Runs RabbitMQ container locally |
| `Aspire.Hosting.Kafka` | Runs Kafka container locally |
| `Aspire.Hosting.Elasticsearch` | Runs Elasticsearch container locally |

```csharp
// These Azure resources default to real Azure — use .RunAsEmulator() for local dev
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

var cosmos = builder.AddAzureCosmosDB("cosmos").RunAsEmulator();
var cosmosDb = cosmos.AddDatabase("projectdb");

var serviceBus = builder.AddAzureServiceBus("messaging").RunAsEmulator();
serviceBus.AddQueue("order-queue");

var eventHubs = builder.AddAzureEventHubs("events").RunAsEmulator();
eventHubs.AddEventHub("telemetry-hub");

// These already run local containers — no .RunAsEmulator() needed
var sql = builder.AddSqlServer("sql");
var redis = builder.AddRedis("redis");
var rabbit = builder.AddRabbitMQ("rabbit");
```

#### Standalone Local Emulators (Not Aspire-Managed)

Many Azure and third-party services ship **standalone emulators** that can run locally — even without an Aspire hosting package. These are valuable when:
- The project doesn't use Aspire at all
- An Aspire `.RunAsEmulator()` option doesn't exist for the service
- You want to run the emulator outside of the Aspire lifecycle (e.g., always-on in the background)
- You need to test against the emulator from non-.NET tooling (scripts, Postman, etc.)

**Always prefer an Aspire hosting package when available** (simpler lifecycle management), but recommend these standalone emulators when Aspire support is missing or when the team has a reason to manage the emulator independently.

| Azure / Infrastructure Service | Standalone Emulator | How to Run Locally | Notes |
|-------------------------------|--------------------|--------------------|-------|
| **Azure Blob / Table / Queue Storage** | **Azurite** | `npm install -g azurite && azurite -s -l c:\azurite` or Docker: `docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite` | Connection string: `UseDevelopmentStorage=true`. Use Azure Storage Explorer to browse. Also available as VS Code extension. |
| **Azure Cosmos DB** | **Cosmos DB Emulator** | Windows installer: [download from Microsoft](https://learn.microsoft.com/en-us/azure/cosmos-db/emulator) runs as a Windows service on `https://localhost:8081`. Docker: `docker run -p 8081:8081 -p 10250-10255:10250-10255 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest` | Well-known emulator key in connection string. Linux Docker image also available. Data Explorer at `https://localhost:8081/_explorer/`. |
| **Azure Service Bus** | **Service Bus Emulator** | Docker Compose — see [Microsoft docs](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator). Requires SQL Server container as dependency. | Supports queues and topics. Connection string uses `Endpoint=sb://localhost;...`. |
| **Azure Event Hubs** | **Event Hubs Emulator** | Docker Compose — see [Microsoft docs](https://learn.microsoft.com/en-us/azure/event-hubs/overview-emulator). Requires Azurite as checkpoint store. | Supports event hubs and consumer groups. |
| **Azure SQL / SQL Server** | **SQL Server LocalDB** | Installed with Visual Studio or SQL Server Express. No container needed. `(localdb)\MSSQLLocalDB` | Zero-config for Windows devs. No Docker required. Great for simple local dev without Aspire. |
| **Azure SQL / SQL Server** | **SQL Server Docker** | `docker run -e ACCEPT_EULA=Y -e SA_PASSWORD=... -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest` | Cross-platform. Use when LocalDB isn't available (macOS/Linux). |
| **Azure SignalR** | **Self-hosted SignalR** | Built into ASP.NET Core — `app.MapHub<MyHub>("/hub")` | No emulator needed. Self-hosted mode works locally out of the box; Azure SignalR is only needed at scale. |
| **Azure Functions** | **Azure Functions Core Tools** | `npm install -g azure-functions-core-tools@4` then `func start` | Runs Functions runtime locally. Requires Azurite for storage trigger/binding support. |
| **Azure Key Vault** | **None** | Use `dotnet user-secrets` or `appsettings.Development.json` | No emulator exists. User-secrets is the standard local replacement. |
| **Azure AI Search** | **None** | Use Elasticsearch Docker container locally, or stub the search interface with an in-memory implementation | No emulator exists. Elasticsearch is the closest local alternative. |
| **Azure Application Insights** | **None (use alternatives)** | Use Aspire dashboard (built-in), Seq (`docker run -p 5341:80 datalust/seq`), or Jaeger (`docker run -p 16686:16686 jaegertracing/all-in-one`) | No emulator exists. Aspire dashboard already provides metrics/traces/logs locally. Seq adds structured log search. |
| **Azure OpenAI / OpenAI** | **Ollama** | `docker run -p 11434:11434 ollama/ollama` then `ollama pull llama3` | Run open-weight models locally. Compatible with OpenAI client SDK via base URL override. |
| **Azure Redis Cache** | **Redis Docker** | `docker run -p 6379:6379 redis:latest` or install Redis via WSL/package manager | Redis itself is the emulator — no Azure-specific emulator needed. |
| **RabbitMQ** | **RabbitMQ Docker** | `docker run -p 5672:5672 -p 15672:15672 rabbitmq:management` | Management UI at `http://localhost:15672` (guest/guest). |

> **Tip:** Many of these Docker-based emulators can be wired into Aspire as generic container resources using `builder.AddContainer(name, image)` even when no dedicated `Aspire.Hosting.*` package exists. This gives you Aspire dashboard visibility and lifecycle management without needing an official integration.

#### Services With No Emulator at All — Fallback Strategies

When neither an Aspire hosting package nor a standalone emulator exists:

| Azure Service | Fallback for Local Dev |
|---------------|----------------------|
| **Azure Key Vault** | Use `dotnet user-secrets` in Development. In `Bootstrapper`, conditionally skip Key Vault registration when `IHostEnvironment.IsDevelopment()`. |
| **Azure AI Search** | Interface-based abstraction + in-memory stub implementing `ISearchService`. Or run local Elasticsearch as a stand-in. |
| **Azure Front Door / CDN** | Not applicable locally — test directly against Gateway or API. |
| **Azure Managed Identity** | Use `DefaultAzureCredential` which falls through to Azure CLI / VS credential locally. No emulator needed. |

#### Decision Flowchart for Adding an Infrastructure Dependency

```
Need a service locally?
  │
  ├─ 1. Search NuGet: Aspire.Hosting.{ServiceName}
  │   ├─ Found → Install package, use builder.Add{Service}(...)
  │   │   └─ Azure variant with .RunAsEmulator()? → Use it (best option)
  │   │
  │   └─ Not found ↓
  │
  ├─ 2. Is there a standalone local emulator?
  │   ├─ Yes (e.g., Azurite, Cosmos Emulator, Functions Core Tools)
  │   │   ├─ Docker image available? → Wire into Aspire: builder.AddContainer(...)
  │   │   └─ Windows installer / npm tool? → Run outside Aspire, configure connection string in appsettings.Development.json
  │   │
  │   └─ Not found ↓
  │
  └─ 3. No emulator exists
      ├─ Can self-host? (e.g., SignalR) → Use self-hosted mode locally
      ├─ Can substitute? (e.g., Elasticsearch for AI Search, Seq for App Insights) → Use substitute
      └─ Neither → Stub/mock the interface + use dev config (user-secrets, appsettings.Development.json)
  ```

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

### Aspire.Hosting.AppHost Package

The AppHost `.csproj` **must** include `Aspire.Hosting.AppHost` as a `PackageReference`. This package provides the dashboard, orchestration metadata, and CLI tooling.

```xml
<PackageReference Include="Aspire.Hosting.AppHost" />
```

If the AppHost fails with errors about missing dashboard paths or orchestration metadata, verify this package reference is present and the version is up to date.

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
| Missing `CliPath` or `DashboardPath` at startup | `Aspire.Hosting.AppHost` package missing from `.csproj` | Add `<PackageReference Include="Aspire.Hosting.AppHost" />` to AppHost.csproj |
| "Aspire Workload has been deprecated" build error | Legacy `<Sdk Name="Aspire.AppHost.Sdk" ... />` stanza present | Remove the `<Sdk>` stanza entirely. Use `<PackageReference Include="Aspire.Hosting.AppHost" />` instead. See [upgrade guide](https://aka.ms/aspire/update-to-sdk). |
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
