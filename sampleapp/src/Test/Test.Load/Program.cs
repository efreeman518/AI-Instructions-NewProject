// ═══════════════════════════════════════════════════════════════
// Pattern: Load Test Console App — entry point for NBomber test execution.
//
// This is NOT an MSTest project — NBomber runs as a console application
// that outputs HTML and CSV reports to the ./reports/ directory.
//
// Usage:
//   dotnet run                           # Run with default appsettings.json
//   dotnet run -- --config custom.json   # Override config file
//
// The app loads configuration from appsettings.json, acquires an auth token
// (if needed), and delegates to TodoItemLoadTest.Run() for scenario execution.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Configuration;
using Test.Load;

// Pattern: Build configuration from appsettings.json.
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddCommandLine(args)
    .Build();

Console.WriteLine("═══════════════════════════════════════════════════");
Console.WriteLine("  TaskFlow Load Tests (NBomber)");
Console.WriteLine($"  Target: {config["BaseUrl"]}");
Console.WriteLine("═══════════════════════════════════════════════════");
Console.WriteLine();

// Pattern: Run the load test scenarios.
TodoItemLoadTest.Run(config);
