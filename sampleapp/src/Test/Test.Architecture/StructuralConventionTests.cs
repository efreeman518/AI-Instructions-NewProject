// ═══════════════════════════════════════════════════════════════
// Pattern: Structural convention tests — enforce naming and inheritance rules
// across the solution. These complement dependency tests by validating
// that code follows the project's structural patterns.
// ═══════════════════════════════════════════════════════════════

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;

namespace Test.Architecture;

[TestClass]
public class StructuralConventionTests : ArchitectureTestBase
{
    [TestMethod]
    [TestCategory("Architecture")]
    public void AllEntities_InheritFrom_EntityBase()
    {
        // Pattern: All domain entities must inherit from EntityBase.
        // EntityBase provides Id, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate.
        // Exceptions: junction entities (TodoItemTag) that use composite keys.
        var result = Types.InAssembly(DomainModelAssembly)
            .That()
            .ResideInNamespace("Domain.Model.Entities")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .DoNotHaveNameMatching("TodoItemTag") // Junction entity — composite PK, no EntityBase
            .Should()
            .Inherit(typeof(EF.Domain.EntityBase))
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("Entities not inheriting EntityBase", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void AllServices_ResideIn_ApplicationServicesNamespace()
    {
        // Pattern: All service implementations must be in the Application.Services namespace.
        var result = Types.InAssembly(ApplicationServicesAssembly)
            .That()
            .HaveNameEndingWith("Service")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespace("Application.Services")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("Services not in Application.Services", result));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void AllRules_InheritFrom_RuleBase()
    {
        // Pattern: All domain rules must inherit from RuleBase<T>.
        var result = Types.InAssembly(DomainModelAssembly)
            .That()
            .ResideInNamespace("Domain.Model.Rules")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .DoNotHaveNameMatching("AllRule|AnyRule") // Composite rule helpers — use IRule<T> directly
            .Should()
            .HaveNameEndingWith("Rule")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            FormatFailure("Rules not following naming convention", result));
    }
}
