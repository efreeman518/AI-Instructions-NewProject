// ═══════════════════════════════════════════════════════════════
// Pattern: TodoItem Service Benchmarks — measures application service layer performance.
//
// Uses BenchmarkDotNet to benchmark CRUD operations through the service interface.
// Dependencies are stubbed with mock implementations to isolate service logic.
//
// Key benchmarking patterns:
// 1. [GlobalSetup] wires dependencies once per run
// 2. [Benchmark] methods test individual operations
// 3. [Params] varies input sizes for scaling analysis
// 4. Mock repositories return contrived data (no DB overhead)
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Repositories;
using Application.Contracts.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Domain.Model;
using Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Test.Support;

namespace Test.Benchmarks;

/// <summary>
/// Pattern: BenchmarkDotNet class — benchmarks TodoItem service operations.
/// Uses IntegrationTestBase-style DI for realistic service construction.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class TodoItemBenchmarks
{
    private ITodoItemService _service = null!;
    private IServiceProvider _serviceProvider = null!;

    [Params(10, 100, 1000)]
    public int ItemCount { get; set; }

    /// <summary>
    /// Pattern: GlobalSetup — construct the service once per benchmark parameter set.
    /// Uses InMemoryDbBuilder to seed test data.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Pattern: Build a lightweight DI container with in-memory DB.
        var services = new ServiceCollection();

        var config = Utility.BuildConfiguration(
            ("ConnectionStrings:TaskFlowDb", "Data Source=:memory:"));

        // Pattern: Seed mock data into in-memory DB for repository benchmarks.
        var dbBuilder = new InMemoryDbBuilder()
            .SeedDefaultEntityData();

        // Pattern: Resolve the service from DI.
        // Note: In a real benchmark, you'd fully wire DI via Bootstrapper.
        // For this sample, we demonstrate the benchmark scaffolding pattern.
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>Benchmark: Service-layer search operation.</summary>
    [Benchmark(Description = "Search TodoItems")]
    public async Task SearchAsync()
    {
        // Pattern: Benchmark measures throughput of the search path.
        // In a full implementation, this calls _service.SearchAsync(filter).
        // For this sample, we demonstrate the benchmark method structure.
        await Task.CompletedTask;
    }

    /// <summary>Benchmark: Service-layer GetById operation.</summary>
    [Benchmark(Description = "GetById TodoItem")]
    public async Task GetByIdAsync()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        // Pattern: _service.GetByIdAsync(id, CancellationToken.None);
        await Task.CompletedTask;
    }

    /// <summary>Benchmark: Service-layer Create operation.</summary>
    [Benchmark(Description = "Create TodoItem")]
    public async Task CreateAsync()
    {
        // Pattern: _service.CreateAsync(dto, CancellationToken.None);
        await Task.CompletedTask;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
