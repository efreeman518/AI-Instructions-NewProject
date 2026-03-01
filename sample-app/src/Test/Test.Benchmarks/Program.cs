using BenchmarkDotNet.Running;
using Test.Benchmarks;

// https://github.com/dotnet/BenchmarkDotNet
Console.WriteLine("TaskFlow Benchmarks");

BenchmarkRunner.Run([
    typeof(TodoItemBenchmarks),
    typeof(ValidationBenchmarks)
]);
