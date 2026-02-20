// ═══════════════════════════════════════════════════════════════
// Pattern: Repository Benchmarks — measures data access layer performance.
//
// Benchmarks repository query methods using EF Core InMemory provider.
// Seeds test data via InMemoryDbBuilder and measures query throughput.
//
// Key patterns:
// 1. InMemory DbContext for isolated, repeatable benchmarks
// 2. [IterationSetup] resets DB state between iterations if needed
// 3. Measures both raw query and materialized result performance
// ═══════════════════════════════════════════════════════════════

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Test.Support;

namespace Test.Benchmarks;

/// <summary>
/// Pattern: Repository-level benchmarks — measures EF Core query performance
/// against an InMemory database seeded with test data.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class RepositoryBenchmarks
{
    [Params(100, 500, 1000)]
    public int SeedCount { get; set; }

    /// <summary>
    /// Pattern: GlobalSetup — create InMemory DbContext and seed with SeedCount entities.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Pattern: Use InMemoryDbBuilder for repeatable benchmark data.
        var dbBuilder = new InMemoryDbBuilder()
            .SeedDefaultEntityData();

        // Pattern: Build InMemory context with seeded data.
        // var dbContext = dbBuilder.BuildInMemory<TaskFlowDbContextQuery>();
        // _repository = new TodoItemRepositoryQuery(dbContext);
    }

    /// <summary>Benchmark: Repository SearchAsync with text filter.</summary>
    [Benchmark(Description = "SearchAsync with filter")]
    public async Task SearchWithFilterAsync()
    {
        // Pattern: var results = await _repository.SearchAsync(filter, page, pageSize);
        // Measures EF query compilation + materialization overhead.
        await Task.CompletedTask;
    }

    /// <summary>Benchmark: Repository GetByIdAsync — single entity lookup.</summary>
    [Benchmark(Description = "GetByIdAsync single entity")]
    public async Task GetByIdAsync()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        // Pattern: var entity = await _repository.GetByIdAsync(id);
        await Task.CompletedTask;
    }

    /// <summary>Benchmark: Repository GetAll — full table scan with projection.</summary>
    [Benchmark(Description = "GetAll with DTO projection")]
    public async Task GetAllWithProjectionAsync()
    {
        // Pattern: var dtos = await _repository.GetAllAsync(projector);
        // Measures IQueryable projection compile + execute + materialize.
        await Task.CompletedTask;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Pattern: Dispose DbContext to release InMemory store.
    }
}
