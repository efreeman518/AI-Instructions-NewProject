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

---

## AppHost Baseline Pattern

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sql", password, port: 38433)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("{project}-sql-data");
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

await builder.Build().RunAsync();
```

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
2. Keep scheduler single replica when using TickerQ.
3. Use `WaitFor(...)` for startup ordering dependencies.
4. Keep Gateway as public ingress, backend hosts internal.
5. Keep AppHost resource names aligned with IaC modules in [iac.md](iac.md).

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

When using `Aspire.Hosting.Azure.Sql`, `Microsoft.Data.SqlClient 6.0.1` pulls `Microsoft.IdentityModel.JsonWebTokens` at a version that conflicts with other Aspire dependencies. To resolve NU1605:

1. Pin in `Directory.Packages.props`:
   ```xml
   <PackageVersion Include="Microsoft.IdentityModel.JsonWebTokens" Version="7.7.1" />
   ```
2. Add a redundant `<PackageReference>` for this package in the AppHost `.csproj` to suppress the downgrade warning.

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
<Project Sdk="Aspire.AppHost.Sdk/9.2.0">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.SqlServer" />
    <PackageReference Include="Aspire.Hosting.Redis" />
  </ItemGroup>
</Project>
```

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
