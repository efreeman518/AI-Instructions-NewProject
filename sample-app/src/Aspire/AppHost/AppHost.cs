var builder = DistributedApplication.CreateBuilder(args);

// Shared infrastructure
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume("taskflow-sql-data")
    .AddDatabase("TaskFlowDb");

var redis = builder.AddRedis("redis")
    .WithDataVolume("taskflow-redis-data");

// TaskFlow API
var taskflowApi = builder.AddProject<Projects.TaskFlow_Api>("taskflowapi")
    .WithReference(sql)
    .WithReference(redis)
    .WithHttpEndpoint(port: 5065, name: "http-api")
    .WithHttpsEndpoint(port: 7065, name: "https-api")
    .WaitFor(sql)
    .WaitFor(redis);

// TaskFlow Scheduler
var taskflowScheduler = builder.AddProject<Projects.TaskFlow_Scheduler>("taskflowscheduler")
    .WithReference(sql)
    .WithReference(redis)
    .WithHttpEndpoint(port: 5100, name: "http-scheduler")
    .WithHttpsEndpoint(port: 7100, name: "https-scheduler")
    .WithReplicas(1)
    .WaitFor(sql)
    .WaitFor(redis);

// TaskFlow Gateway (YARP)
var taskflowGateway = builder.AddProject<Projects.TaskFlow_Gateway>("taskflowgateway")
    .WithReference(taskflowApi)
    .WithHttpEndpoint(port: 5028, name: "http-gateway")
    .WithHttpsEndpoint(port: 7028, name: "https-gateway")
    .WaitFor(taskflowApi);

// Function App
var functionApp = builder.AddProject<Projects.FunctionApp>("functionapp")
    .WaitFor(sql);

builder.Build().Run();
