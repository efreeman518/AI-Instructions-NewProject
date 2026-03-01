using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Domain.Model;
using Domain.Shared;

namespace Test.Benchmarks;

/// <summary>
/// Benchmarks for TodoItem domain entity creation, state transitions, and validation.
/// Modeled after https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Benchmarks/TodoItemBenchmarks.cs
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class TodoItemBenchmarks
{
    [Params(5, 10)]
    public int TitleLength { get; set; }

    private string _title = null!;
    private readonly Guid _tenantId = Guid.NewGuid();

    [IterationSetup]
    public void Setup()
    {
        _title = $"a{RandomString(TitleLength)}";
    }

    [Benchmark(Baseline = true)]
    public object CreateTodoItem()
    {
        var result = TodoItem.Create(_tenantId, _title);
        return result;
    }

    [Benchmark]
    public object CreateAndStartTodoItem()
    {
        var result = TodoItem.Create(_tenantId, _title);
        if (result.IsSuccess)
            result.Value!.Start();
        return result;
    }

    [Benchmark]
    public object CreateStartCompleteTodoItem()
    {
        var result = TodoItem.Create(_tenantId, _title);
        if (result.IsSuccess)
        {
            var item = result.Value!;
            item.Start();
            item.Complete();
        }
        return result;
    }

    [Benchmark]
    public object CreateAndUpdateTodoItem()
    {
        var result = TodoItem.Create(_tenantId, _title, "Some description", priority: 2);
        if (result.IsSuccess)
            result.Value!.Update(title: $"updated-{_title}");
        return result;
    }

    private static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
