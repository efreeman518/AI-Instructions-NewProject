// ═══════════════════════════════════════════════════════════════
// Pattern: API layer architecture tests — validates that the API host
// works with DTOs (Application.Models) via service interfaces (Application.Contracts),
// NOT by reaching into Domain entities or EF Core directly.
// ═══════════════════════════════════════════════════════════════

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;

namespace Test.Architecture;

[TestClass]
public class ApiDependencyTests : ArchitectureTestBase
{
    [TestMethod]
    [TestCategory("Architecture")]
    public void Api_HasNoDependencyOn_DomainEntities()
    {
        // Pattern: API endpoints work with DTOs, not domain entities.
        // The API project references Application.Contracts/Models, not Domain.Model.
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Domain.Model.Entities", "Domain.Model.Rules")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("API → Domain entities", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void Api_HasNoDependencyOn_EntityFrameworkCore()
    {
        // Pattern: API layer should not reference EF Core directly.
        // Database access is through services → repositories (DI).
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("API → EF Core", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void Api_HasNoDependencyOn_InfrastructureData()
    {
        // Pattern: API should not reference Infrastructure.Repositories directly.
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Infrastructure.Repositories", "Infrastructure.Notification",
                "TaskFlow.Infrastructure.Repositories")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("API → Infrastructure.Data", result));
    }
}
