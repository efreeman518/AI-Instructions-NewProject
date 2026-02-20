// ═══════════════════════════════════════════════════════════════
// Pattern: Domain layer architecture tests — validates Clean Architecture's
// innermost ring has ZERO dependencies on outer layers.
// Domain.Model must not reference Application, Infrastructure, or EF Core.
// Domain.Shared must not reference Domain.Model or outer layers.
// ═══════════════════════════════════════════════════════════════

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;

// Pattern: Architecture tests are stateless — max parallel, method-level scope.
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

namespace Test.Architecture;

/// <summary>
/// Pattern: Shared base class — loads assemblies once by referencing a known type from each layer.
/// All architecture test classes inherit from this base to reuse assembly references.
/// </summary>
public abstract class ArchitectureTestBase
{
    /// <summary>Assembly containing domain entities (TodoItem, Category, etc.).</summary>
    protected static readonly Assembly DomainModelAssembly =
        typeof(Domain.Model.Entities.TodoItem).Assembly;

    /// <summary>Assembly containing shared constants (Roles, CacheNames, EventNames).</summary>
    protected static readonly Assembly DomainSharedAssembly =
        typeof(Domain.Shared.Constants.Roles).Assembly;

    /// <summary>Assembly containing application services (TodoItemService, etc.).</summary>
    protected static readonly Assembly ApplicationServicesAssembly =
        typeof(Application.Services.TodoItemService).Assembly;

    /// <summary>Assembly containing the API host (Program.cs, endpoints).</summary>
    protected static readonly Assembly ApiAssembly =
        typeof(Program).Assembly;

    /// <summary>Format failure message with failing type details.</summary>
    protected static string FormatFailure(string rule, TestResult result)
    {
        return result.FailingTypeNames is not null
            ? $"{rule} violation: {string.Join(", ", result.FailingTypeNames)}"
            : $"{rule} violation (no type details available)";
    }
}

[TestClass]
public class DomainDependencyTests : ArchitectureTestBase
{
    [TestMethod]
    [TestCategory("Architecture")]
    public void DomainModel_HasNoDependencyOn_Application()
    {
        // Pattern: Domain.Model must not reference Application layer.
        var result = Types.InAssembly(DomainModelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Application.Contracts", "Application.Services", "Application.Models")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful, FormatFailure("Domain.Model → Application", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void DomainModel_HasNoDependencyOn_Infrastructure()
    {
        // Pattern: Domain.Model must not reference Infrastructure layer.
        var result = Types.InAssembly(DomainModelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Infrastructure", "Infrastructure.Repositories", "Infrastructure.Notification")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful, FormatFailure("Domain.Model → Infrastructure", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void DomainModel_HasNoDependencyOn_EntityFrameworkCore()
    {
        // Pattern: Domain must be persistence-ignorant — no EF Core references.
        var result = Types.InAssembly(DomainModelAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful, FormatFailure("Domain.Model → EntityFrameworkCore", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void DomainShared_HasNoDependencyOn_DomainModel()
    {
        // Pattern: Domain.Shared (constants) should not reference Domain.Model.
        // This ensures Domain.Shared can be referenced without pulling in entities.
        var result = Types.InAssembly(DomainSharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Domain.Model", "Application", "Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("Domain.Shared → Domain.Model/Application/Infrastructure", result));
    }
}
