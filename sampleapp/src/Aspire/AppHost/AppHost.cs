// ═══════════════════════════════════════════════════════════════
// Pattern: Aspire AppHost — local development orchestration.
// Wires up SQL Server, Redis, API, Scheduler, Gateway with
// service discovery, persistent volumes, and health-based startup.
// ═══════════════════════════════════════════════════════════════

var builder = DistributedApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════
// Infrastructure Resources
// ═══════════════════════════════════════════════════════════════

// Pattern: SQL Server with persistent data volume.
// Password stored as a secret parameter — appsettings.json "Parameters:aspire-sql-password".
var password = builder.AddParameter("aspire-sql-password", secret: true);
var sqlServer = builder.AddSqlServer("sql", password, port: 38433)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("taskflow-sql-data");

// Pattern: Named database on the SQL Server container.
var taskflowDb = sqlServer.AddDatabase("taskflowdb");

// Pattern: Redis for FusionCache L2 + backplane.
var redis = builder.AddRedis("redis");

// ═══════════════════════════════════════════════════════════════
// Application Projects
// ═══════════════════════════════════════════════════════════════

// Pattern: API — receives both read/write DbContext connection strings (same DB, different names).
// Redis for FusionCache L2 distributed cache + backplane.
var api = builder.AddProject<Projects.TaskFlow_Api>("taskflowapi")
    .WithHttpEndpoint(port: 5065, name: "http-api")
    .WithHttpsEndpoint(port: 7065, name: "https-api")
    .WithReference(taskflowDb, connectionName: "TaskFlowDbContextTrxn")
    .WithReference(taskflowDb, connectionName: "TaskFlowDbContextQuery")
    .WithReference(redis, connectionName: "Redis1")
    .WaitFor(sqlServer)
    .WaitFor(redis);

// Pattern: Scheduler — single replica to prevent duplicate job execution.
// Receives main DB for domain services + SchedulerDbContext for TickerQ persistence.
// Rule: WithReplicas(1) is CRITICAL unless Redis coordination is enabled.
var scheduler = builder.AddProject<Projects.TaskFlow_Scheduler>("taskflowscheduler")
    .WithHttpEndpoint(port: 5100, name: "http-scheduler")
    .WithHttpsEndpoint(port: 7100, name: "https-scheduler")
    .WithReference(taskflowDb, connectionName: "TaskFlowDbContextTrxn")
    .WithReference(taskflowDb, connectionName: "TaskFlowDbContextQuery")
    .WithReference(taskflowDb, connectionName: "SchedulerDbContext")
    .WaitFor(sqlServer)
    .WithReplicas(1);

// Pattern: Gateway — reverse proxy, starts AFTER API is healthy.
// WaitFor ensures Gateway doesn't accept requests until API health checks pass.
var gateway = builder.AddProject<Projects.TaskFlow_Gateway>("taskflowgateway")
    .WithHttpEndpoint(port: 5028, name: "http-gateway")
    .WithHttpsEndpoint(port: 7028, name: "https-gateway")
    .WithReference(api)
    .WithReference(scheduler)
    .WaitFor(api);

// Pattern: Functions — optional, uncomment when Functions are promoted past template stage.
// builder.AddProject<Projects.TaskFlow_FunctionApp>("taskflowfunctions")
//     .WithReference(taskflowDb, connectionName: "TaskFlowDbContextTrxn")
//     .WithReference(redis, connectionName: "Redis1");

await builder.Build().RunAsync();
