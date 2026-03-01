using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Domain.Model;
using Domain.Shared;

namespace Test.Benchmarks;

/// <summary>
/// Benchmarks for domain entity validation — similar to the ValidatorBenchmarks pattern
/// from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Benchmarks/ValidatorBenchmarks.cs
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ValidationBenchmarks
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Benchmark]
    public object ValidTodoItem_Create()
    {
        return TodoItem.Create(_tenantId, $"Valid-{Guid.NewGuid()}", "A description");
    }

    [Benchmark]
    public object InvalidTodoItem_EmptyTitle()
    {
        return TodoItem.Create(_tenantId, "", "A description");
    }

    [Benchmark]
    public object InvalidTodoItem_TitleTooLong()
    {
        var longTitle = new string('x', DomainConstants.TITLE_MAX_LENGTH + 1);
        return TodoItem.Create(_tenantId, longTitle);
    }

    [Benchmark]
    public object InvalidTodoItem_NegativeHours()
    {
        var result = TodoItem.Create(_tenantId, $"Valid-{Guid.NewGuid()}");
        if (result.IsSuccess)
            result.Value!.Update(estimatedHours: -1m);
        return result;
    }

    [Benchmark]
    public object ValidTodoItem_FullLifecycle()
    {
        var result = TodoItem.Create(_tenantId, $"Valid-{Guid.NewGuid()}");
        if (result.IsSuccess)
        {
            var item = result.Value!;
            item.Start();
            item.Complete();
            item.Archive();
        }
        return result;
    }
}
