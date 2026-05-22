# Aspire Orchestration

Use Aspire AppHost for local orchestration and keep it consistent with IaC outputs.

Reference patterns: [../patterns/infrastructure-wiring.md](../patterns/infrastructure-wiring.md) (Aspire Resource Wiring).

## Structure

```text
Host/Aspire/
  AppHost/
    AppHost.cs
    AppHost.csproj
  ServiceDefaults/
    Extensions.cs
```

### AppHost Entry File: `AppHost.cs` (not `Program.cs`)

The AppHost project's entry file is **`AppHost.cs`** — the Aspire 13 convention. Use this name even on Aspire 9.x SDKs; it is purely a file rename and is back-compatible:

- Top-level statements emit an implicit `class Program` regardless of file name, so reflective lookups like `Type.GetType("Program, AppHost", ...)` and `WebApplicationFactory<Program>` keep working.
- No code references need updating. `.csproj` only needs editing if it has explicit `<Compile>` items (default item-includes do not).
- "`Program.cs` in the AppHost project" reads as a generic ASP.NET Core entry point — `AppHost.cs` correctly signals an Aspire orchestrator. Humans grep by file name; align it with intent.

> **Single-file AppHost** (a single `apphost.cs` lowercase with `#:sdk` / `#:package` directives, no `.csproj`) is a separate Aspire 13 prototype-only feature. Not adopted; not supported in Visual Studio.

---

## AppHost Baseline Pattern

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sql", password, port: 38433)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("{project}-sql-data")
    .WithImageTag("2025-latest");
var projectDb = sqlServer.AddDatabase("{project}db");

var redis = builder.AddRedis("redis");

var api = builder.AddProject<Projects.{Host}_Api>("{host}api")
    .WithReference(projectDb, connectionName: "{Project}DbContextTrxn")
    .WithReference(projectDb, connectionName: "{Project}DbContextQuery")
    .WithReference(redis, connectionName: "Redis1")
    .WaitFor(sqlServer)
    .WaitFor(redis);

var scheduler = builder.AddProject<Projects.{Host}_Scheduler>("{host}scheduler")
    .WithReference(projectDb, connectionName: "{Project}DbContextTrxn")
    .WithReference(projectDb, connectionName: "{Project}DbContextQuery")
    .WithReference(projectDb, connectionName: "SchedulerDbContext")
    .WithReplicas(1)
    .WaitFor(sqlServer);

var gateway = builder.AddProject<Projects.{Gateway}_Gateway>("{gateway}")
    .WithReference(api)
    .WithReference(scheduler)
    .WaitFor(api);

builder.AddViteApp("{host}react", "../../../UI/{Project}.React")
    .WithReference(gateway)
    .WithEnvironment("VITE_API_BASE_URL", gateway.GetEndpoint("http"))
    .WaitFor(gateway)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
```

Only include `AddViteApp(...)` when `includeReactUI: true`. If Gateway is disabled, reference the API project and pass the API endpoint to `VITE_API_BASE_URL` instead. Aspire may assign a dynamic Vite port; read the resource URL from the current dashboard/console output for browser tests.

---

## ServiceDefaults Pattern

```csharp
public static IHostApplicationBuilder AddServiceDefaults(
    this IHostApplicationBuilder builder,
    IConfiguration config,
    string appName)
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

---

## Hosting Package Discovery Rule

When adding infra dependencies:
1. Check for `Aspire.Hosting.{Service}` package first.
2. If Azure service supports emulator mode, use `.RunAsEmulator()`.
3. If no official package exists, use local emulator/container or stub.

Examples:

```csharp
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var cosmos = builder.AddAzureCosmosDB("cosmos").RunAsEmulator();
var serviceBus = builder.AddAzureServiceBus("servicebus").RunAsEmulator();
var eventHubs = builder.AddAzureEventHubs("eventhubs").RunAsEmulator();
```

> **Do NOT use `ContainerLifetime.Persistent` on Azure emulator containers** (Storage, Service Bus, Cosmos, Event Hubs). Persistent emulator containers survive Aspire restarts but get stranded on deleted Podman/Docker networks, causing `netavark "eth2 already exists"` errors and broken restarts. Only SQL Server and Redis use `Persistent` + named volumes — Azure emulators should use the default ephemeral lifetime.

### Azure Service Bus Topics and Subscriptions

```csharp
var sb = builder.AddAzureServiceBus("servicebus").RunAsEmulator();
sb.AddTopic("domain-events", ["api", "other-subscriber"]);
sb.AddQueue("commands"); // optional queue
```

> **API note (Aspire 9.3.0+):** Use `AddTopic(name, subscriptions[])` — the chained `.AddServiceBusTopic().AddServiceBusSubscription()` API does not exist. Queues use `AddQueue(name)`.

Services like `AddSqlServer`, `AddRedis`, `AddPostgres`, `AddRabbitMQ` already run local containers by default.

---

## Key Rules

1. `WithReference(..., connectionName: "X")` must map to runtime key `ConnectionStrings:X`.
2. **Every project that needs a resource must have its own `.WithReference()` call.** A common bug: adding a new consumer project (e.g., Functions) that uses the same database as the API but forgetting to add `.WithReference(db, connectionName: "...")`. The project silently falls back to `appsettings.json` connection strings (often LocalDB or nonexistent), causing connection errors that look like infrastructure problems.
3. Keep scheduler single replica when using TickerQ.
4. Use `WaitFor(...)` for startup ordering dependencies.
5. Keep Gateway as public ingress, backend hosts internal.
6. Keep AppHost resource names aligned with IaC modules in [iac.md](iac.md).

---

## Parameter Resolution and Credential Management

### Aspire Parameter Resolution Order

Aspire resolves `AddParameter` values in this priority order (highest wins):

1. Environment variables (`Parameters__<name>`)
2. `appsettings.{Environment}.json` entries under `Parameters:<name>`
3. `AddParameter(..., default: ...)` code default

**A value in `appsettings.Development.json` or `appsettings.Testing.json` silently overrides the code default on every run.** This is the most common source of "why isn't my password change taking effect?" bugs.

### Rules

- **Never put `Parameters:sql-password` (or any credential parameter) in any AppHost `appsettings` file.** It overrides everything silently. Keep those files as `{}` or omit the `Parameters` key entirely.
- **Define passwords as a single shared constant** (e.g., `LocalSqlSettings.SharedSaPassword`). Use that constant as the `AddParameter` default and in test fixture setup. Change in one place only.
- **Persistent SQL volumes lock in the SA password at volume creation time.** If you change the password constant, you must delete the named volume (e.g., `taskflow-sql-data`) before the next run — the container will re-initialize with the new password.
- **Killing the AppHost process does not stop Docker/Podman containers.** Clean up persistent containers explicitly:
  ```bash
  docker ps --filter label=com.microsoft.dotnet.aspire.container.name --format "{{.ID}}" | xargs docker rm -f
  ```
  Or remove by name/label from the Aspire dashboard or `docker rm -f <container-name>`.

### Pattern: Shared Constant + Clean appsettings

```csharp
// AppHost/LocalSqlSettings.cs
public static class LocalSqlSettings
{
    public const string SharedSaPassword = "YourStr0ngP@ssword!";
}

// AppHost/Program.cs
var sqlPassword = builder.AddParameter("sql-password", LocalSqlSettings.SharedSaPassword, secret: true);
var sqlServer = builder.AddSqlServer("sql", sqlPassword)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("{project}-sql-data");
```

`AppHost/appsettings.Development.json` and `appsettings.Testing.json` must **not** contain a `Parameters` section. Leave them as `{}`.

---

## Package Source Mapping for Aspire Dependencies

When the project uses `nuget.config` with `<packageSourceMapping>`, the following patterns must be mapped to `nuget.org` or Aspire transitive restores will fail:

```xml
<packageSource key="nuget.org">
  <package pattern="AspNetCore.HealthChecks.*" />
  <!-- ... existing patterns ... -->
</packageSource>
```

`Aspire.Hosting.*` and `Aspire.ServiceDefaults` pull `AspNetCore.HealthChecks.UI.*` transitively. Without this entry, `dotnet restore` fails with NU1100.

---

## Azure SQL Transitive Version Conflict

When using `Aspire.Hosting.Azure.Sql`, `Microsoft.Data.SqlClient` pulls `Microsoft.IdentityModel.JsonWebTokens` at a version that conflicts with other Aspire dependencies. To resolve NU1605:

1. Pin in `Directory.Packages.props` — **documented exception** to the latest-not-pinned rule (see [package-dependencies.md](package-dependencies.md) → *Latest, Not Pinned*). Resolve the lowest version that satisfies both consumers at scaffold time and keep the inline reason comment:
   ```xml
   <!-- Pinned: NU1605 conflict between Microsoft.Data.SqlClient and other Aspire deps. Re-evaluate on SDK bump. -->
   <PackageVersion Include="Microsoft.IdentityModel.JsonWebTokens" Version="<resolved-at-scaffold>" />
   ```
2. Add a redundant `<PackageReference>` for this package in the AppHost `.csproj` to suppress the downgrade warning.
3. **Re-evaluate on every SDK bump.** When the underlying conflict is resolved upstream, remove the pin and revert to central latest-stable resolution.

---

## Dev Tunnel Pattern (Optional)

```csharp
var tunnel = builder.AddDevTunnel(
    name: "{gateway}-tunnel",
    tunnelId: "{project}-dev-tunnel",
    options: new DevTunnelOptions { AllowAnonymous = false });

tunnel.WithReference(gateway.GetEndpoint("https-gateway"), allowAnonymous: true);
```

Use persistent `tunnelId` for stable callback URLs.

---

## AppHost.csproj Essentials

Use the `Aspire.AppHost.Sdk` MSBuild SDK. It handles `Projects.*` type proxy generation, `IsAspireHost`, and `AspireHostingSDKVersion` automatically.

```xml
<Project Sdk="Aspire.AppHost.Sdk/<latest-stable>">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$(LatestStableTfm)</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.SqlServer" />
    <PackageReference Include="Aspire.Hosting.Redis" />
    <!-- Include only when registering React/Vite UI with AddViteApp. -->
    <PackageReference Include="Aspire.Hosting.JavaScript" />
  </ItemGroup>
</Project>
```

Substitute `<latest-stable>` and the TFM at scaffold time. Do not hard-code versions in templates — see [package-dependencies.md](package-dependencies.md) → *Latest, Not Pinned*.

> **SDK upgrade discipline.** A major Aspire SDK bump (e.g., 9 → 13) is a **deliberate, scheduled task**, not routine work. Aspire 13 tightens a few APIs (e.g., `IDistributedApplicationTestingBuilder` inheritance), and existing code may need adjustments. Consult the official upgrade guide (`learn.microsoft.com/dotnet/aspire/get-started/upgrade-to-aspire-13`) and the version-specific compatibility pages before bumping. The `AppHost.cs` filename convention is back-compatible and may be adopted independently of the SDK bump.

If using dev tunnels, add `Aspire.Hosting.DevTunnels`.

---

## Preflight (Before First Launch)

Before running `dotnet run --project src/Host/Aspire/AppHost`, confirm the substrate:

1. **Docker/Podman running:** `docker info` or `podman info` succeeds. If not, start the container runtime — do not debug app code.
2. **launchSettings.json exists:** AppHost requires `Properties/launchSettings.json` with OTLP/dashboard endpoints. Without it, `dotnet run` starts but the dashboard never opens and the terminal appears blank. Minimal template:
   ```json
   {
     "profiles": {
       "https": {
         "commandName": "Project",
         "dotnetRunMessages": true,
         "launchBrowser": true,
         "applicationUrl": "https://localhost:17179;http://localhost:15180",
         "environmentVariables": {
           "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:21147",
           "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:21147",
           "ASPIRE_ALLOW_UNSECURED_TRANSPORT": "true"
         }
       },
       "http": {
         "commandName": "Project",
         "dotnetRunMessages": true,
         "launchBrowser": true,
         "applicationUrl": "http://localhost:15180",
         "environmentVariables": {
           "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "http://localhost:19197",
           "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL": "http://localhost:19197",
           "ASPIRE_ALLOW_UNSECURED_TRANSPORT": "true"
         }
       }
     }
   }
   ```
3. **User secrets initialized for SQL password:** If the AppHost uses `builder.AddParameter("sql-password", secret: true)`, the secret must exist in user secrets before launch:
   ```powershell
   dotnet user-secrets init --project src/Host/Aspire/AppHost
   dotnet user-secrets set "Parameters:sql-password" "<YourPassword>" --project src/Host/Aspire/AppHost
   ```
   Without this, the SQL container starts but cannot authenticate.
4. **Ports available:** No stale containers holding SQL/Redis ports. Run `docker ps` / `podman ps` to check.
5. **NuGet restore clean:** `dotnet restore` on the AppHost project succeeds (catches `packageSourceMapping` issues before launch).

Only after all five pass, proceed to `dotnet run`.

---

## Run

```bash
dotnet run --project src/Host/Aspire/AppHost
```

If running from CLI without launch profile, set required env vars for dashboard/OTLP endpoints.

---

## Ephemeral URL Discovery

Aspire dashboard URLs, proxy ports, and host endpoints are **assigned at runtime** and may change between launches. Do not carry forward URLs from a previous session.

On each launch:
1. Read the dashboard URL from the `dotnet run` console output.
2. Confirm resource health on the dashboard before testing endpoints.
3. Use the dashboard's resource list to find current host URLs — do not assume prior ports.

When writing `HANDOFF.md`, record the **method to discover URLs** (e.g., "check Aspire dashboard"), not the URLs themselves.

---

## Detecting Aspire at Runtime — Presence, Not Environment

Hosts often need to know "am I being orchestrated by Aspire?" to decide whether to register real Azure clients vs. local no-op stubs. **Gate on the presence of an Aspire-injected connection string, not on `IHostEnvironment.IsEnvironment("Testing")` or any other environment name.**

```csharp
// CORRECT — presence-based
var runningUnderAspire = !string.IsNullOrEmpty(
    builder.Configuration.GetConnectionString("{App}DbContextTrxn"));

if (runningUnderAspire)
{
    builder.AddAzureServiceBusClient("servicebus");
    builder.Services.Replace(
        ServiceDescriptor.Scoped<I{App}EventPublisher, ServiceBus{App}EventPublisher>());
}
```

**Why not environment-name gates.** `WebApplicationFactory<Program>` sets `ASPNETCORE_ENVIRONMENT=Testing` via `UseEnvironment("Testing")`, and `DistributedApplicationTestingBuilder` propagates the same env name to **every child project** it brings up under test. A gate like `!builder.Environment.IsEnvironment("Testing")` therefore returns `false` in **both** WAF tier and Aspire-mesh tier — the API silently keeps its `NoOp` publishers, and integration events are dropped without an error. The bug is invisible (POST 201, row persisted) and only manifests when a downstream consumer fails to observe the event.

The connection-string presence check distinguishes the two tiers correctly: WAF tier injects in-memory DbContext options (no connection string), Aspire tier injects real Aspire-resolved connection strings.

Apply the same principle to Functions hosts, Worker hosts, and Scheduler hosts — any host that has both an Aspire path and a non-Aspire test path. If a host has **no** non-Aspire test path (i.e., always-Aspire), document that explicitly in the host's `Program.cs` and skip the gate entirely.

---

## Debugging Individual Hosts

When a multi-host Aspire run fails, isolate the problem by running hosts standalone:

```powershell
cd src/Host/{Host}.Api
dotnet run 2>&1
```

This bypasses Aspire orchestration and surfaces startup exceptions (DI failures, missing config, migration errors) directly in the console. Fix standalone first, then return to AppHost.

**Common gotcha:** Orphaned `dotnet.exe` processes from prior runs can hold file locks and prevent builds. Check with `Get-Process -Name dotnet` and kill if needed.

---

## Uno.Sdk Incompatibility

Uno.Sdk projects (`<Project Sdk="Uno.Sdk/..."`) do not expose the `GetTargetPath` MSBuild target that Aspire uses for project introspection. Adding an Uno project reference to AppHost causes `MSB4057`. **Comment out the Uno ProjectReference and AddProject call in AppHost.** Run Uno WASM separately.

---

## Verification

- [ ] AppHost starts and dashboard is reachable
- [ ] All resources show "Running" in dashboard (not just "Starting")
- [ ] API/Gateway/Scheduler startup order works (`WaitFor`)
- [ ] SQL/Redis references inject expected connection keys
- [ ] Scheduler runs with single replica
- [ ] Functions listeners start without connection refused errors
- [ ] Aspire resources match IaC resource list
- [ ] Optional emulators/dev tunnel are wired only when needed
