// ═══════════════════════════════════════════════════════════════
// Pattern: TodoItem Load Test — NBomber scenarios for search and CRUD operations.
//
// Demonstrates:
// 1. ScenarioBuilder with step chaining (GET → POST → PUT → DELETE)
// 2. VirtualUser concurrency configuration from appsettings
// 3. Duration-based test runs
// 4. HTTP step factory for common request patterns
// 5. Reporting (HTML + CSV generated automatically)
//
// Run: dotnet run
// Reports: ./reports/ directory after completion
// ═══════════════════════════════════════════════════════════════

using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace Test.Load;

/// <summary>
/// Pattern: NBomber load test definition — defines reusable scenarios.
/// Each scenario represents a user workflow (search, CRUD cycle).
/// </summary>
public static class TodoItemLoadTest
{
    /// <summary>
    /// Pattern: Register all scenarios and run via NBomber.
    /// Called from Program.cs.
    /// </summary>
    public static void Run(IConfiguration config)
    {
        var searchConfig = config.GetSection("Scenarios:Search");
        var createConfig = config.GetSection("Scenarios:Create");

        var searchVU = int.Parse(searchConfig["ConcurrentUsers"] ?? "100");
        var searchDuration = int.Parse(searchConfig["DurationSeconds"] ?? "60");
        var createVU = int.Parse(createConfig["ConcurrentUsers"] ?? "10");
        var createDuration = int.Parse(createConfig["DurationSeconds"] ?? "30");

        // ── Search Scenario ─────────────────────────────────────
        // Pattern: Read-heavy scenario — many concurrent users querying the search endpoint.
        var searchScenario = Scenario.Create("search_todo_items", async context =>
        {
            using var client = Utility.CreateHttpClient(config);

            var request = Http.CreateRequest("GET", "/api/todoitems?search=review")
                .WithHeader("Accept", "application/json")
                .WithHeader("X-Tenant-Id", "00000000-0000-0000-0000-000000000099");

            var response = await Http.Send(client, request);

            return response;
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: searchVU, interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(searchDuration))
        );

        // ── CRUD Scenario ───────────────────────────────────────
        // Pattern: Write scenario — create, read, update, delete lifecycle under load.
        var crudScenario = Scenario.Create("crud_todo_items", async context =>
        {
            using var client = Utility.CreateHttpClient(config);
            var itemId = Guid.NewGuid();

            // Step 1: Create
            var createPayload = new
            {
                Title = $"Load Test Item {itemId:N}",
                Description = "Created by NBomber load test",
                Priority = 2,
                CategoryName = "Testing"
            };

            var createRequest = Http.CreateRequest("POST", "/api/todoitems")
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonContent.Create(createPayload));
            var createResponse = await Http.Send(client, createRequest);

            // Step 2: Read
            var getRequest = Http.CreateRequest("GET", $"/api/todoitems/{itemId}");
            var getResponse = await Http.Send(client, getRequest);

            // Step 3: Update
            var updatePayload = new
            {
                Title = $"Updated Load Test Item {itemId:N}",
                Description = "Updated by NBomber load test",
                Priority = 3,
                CategoryName = "Testing"
            };
            var updateRequest = Http.CreateRequest("PUT", $"/api/todoitems/{itemId}")
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonContent.Create(updatePayload));
            var updateResponse = await Http.Send(client, updateRequest);

            // Step 4: Delete
            var deleteRequest = Http.CreateRequest("DELETE", $"/api/todoitems/{itemId}");
            var deleteResponse = await Http.Send(client, deleteRequest);

            return deleteResponse;
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: createVU, interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(createDuration))
        );

        // ── Run All Scenarios ───────────────────────────────────
        NBomberRunner
            .RegisterScenarios(searchScenario, crudScenario)
            .WithReportFolder("reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .Run();
    }
}
