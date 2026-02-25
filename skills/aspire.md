# Aspire Orchestration

Use Aspire AppHost for local orchestration and keep it consistent with IaC outputs.

Reference implementation: `sampleapp/src/Aspire/AppHost/`, `sampleapp/src/Aspire/ServiceDefaults/`.

## Structure

```text
Aspire/
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

Services like `AddSqlServer`, `AddRedis`, `AddPostgres`, `AddRabbitMQ` already run local containers by default.

---

## Key Rules

1. `WithReference(..., connectionName: "X")` must map to runtime key `ConnectionStrings:X`.
2. Keep scheduler single replica when using TickerQ.
3. Use `WaitFor(...)` for startup ordering dependencies.
4. Keep Gateway as public ingress, backend hosts internal.
5. Keep AppHost resource names aligned with IaC modules in [iac.md](iac.md).

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
  </ItemGroup>
</Project>
```

If using dev tunnels, add `Aspire.Hosting.DevTunnels`.

---

## Run

```bash
dotnet run --project src/Aspire/AppHost
```

If running from CLI without launch profile, set required env vars for dashboard/OTLP endpoints.

---

## Verification

- [ ] AppHost starts and dashboard is reachable
- [ ] API/Gateway/Scheduler startup order works (`WaitFor`)
- [ ] SQL/Redis references inject expected connection keys
- [ ] Scheduler runs with single replica
- [ ] Aspire resources match IaC resource list
- [ ] Optional emulators/dev tunnel are wired only when needed
