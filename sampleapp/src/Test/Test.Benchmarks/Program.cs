// ═══════════════════════════════════════════════════════════════
// Pattern: BenchmarkDotNet Console Entry Point.
//
// Uses BenchmarkSwitcher to allow running specific benchmark classes
// or all benchmarks from the command line.
//
// Usage:
//   dotnet run -c Release                    # Interactive menu
//   dotnet run -c Release -- --filter *Search*  # Run matching benchmarks
//   dotnet run -c Release -- --list flat     # List all available benchmarks
//
// IMPORTANT: Always run in Release configuration for accurate results.
// Debug builds include extra instrumentation that skews measurements.
// ═══════════════════════════════════════════════════════════════

using BenchmarkDotNet.Running;
using Test.Benchmarks;

// Pattern: BenchmarkSwitcher exposes all benchmark classes in the assembly.
// Command-line arguments control which benchmarks execute and how.
BenchmarkSwitcher
    .FromAssembly(typeof(TodoItemBenchmarks).Assembly)
    .Run(args);
