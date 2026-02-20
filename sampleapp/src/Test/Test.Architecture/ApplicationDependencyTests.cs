// ═══════════════════════════════════════════════════════════════
// Pattern: Application layer architecture tests — validates that
// Application.Services depends only on Domain, NOT on Infrastructure or EF Core.
// Application orchestrates domain logic via interfaces (in Contracts);
// Infrastructure inversion happens through DI.
// ═══════════════════════════════════════════════════════════════

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;

namespace Test.Architecture;

[TestClass]
public class ApplicationDependencyTests : ArchitectureTestBase
{
    [TestMethod]
    [TestCategory("Architecture")]
    public void ApplicationServices_HasNoDependencyOn_Infrastructure()
    {
        // Pattern: Application.Services must not reference Infrastructure directly.
        // It depends on IXxxRepository (in Contracts), not on concrete repos.
        var result = Types.InAssembly(ApplicationServicesAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Infrastructure", "Infrastructure.Repositories",
                "Infrastructure.Notification", "TaskFlow.Infrastructure.Data")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("Application → Infrastructure", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void ApplicationServices_HasNoDependencyOn_EntityFrameworkCore()
    {
        // Pattern: Application layer must be ORM-agnostic.
        var result = Types.InAssembly(ApplicationServicesAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("Application → EF Core", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void ApplicationServices_HasNoDependencyOn_Api()
    {
        // Pattern: Application layer must not reference the API host project.
        var result = Types.InAssembly(ApplicationServicesAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("TaskFlow.Api", "Microsoft.AspNetCore")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("Application → API", result));
    }
}
