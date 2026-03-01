using NetArchTest.Rules;

namespace Test.Architecture;

[TestClass]
public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly DomainModelAssembly =
        typeof(Domain.Model.TodoItem).Assembly;

    private static readonly System.Reflection.Assembly DomainSharedAssembly =
        typeof(Domain.Shared.TodoItemStatus).Assembly;

    private static readonly System.Reflection.Assembly ApplicationContractsAssembly =
        typeof(Application.Contracts.Services.ITodoItemService).Assembly;

    private static readonly System.Reflection.Assembly InfrastructureDataAssembly =
        typeof(Infrastructure.Data.TaskFlowDbContextBase).Assembly;

    private static readonly System.Reflection.Assembly InfrastructureRepositoriesAssembly =
        typeof(Infrastructure.Repositories.TodoItemRepositoryQuery).Assembly;

    [TestMethod]
    public void Domain_ShouldNotReference_Infrastructure()
    {
        var result = Types.InAssembly(DomainModelAssembly)
            .ShouldNot()
            .HaveDependencyOn("Infrastructure.Data")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            "Domain.Model must not depend on Infrastructure.Data");
    }

    [TestMethod]
    public void Domain_ShouldNotReference_Application()
    {
        var result = Types.InAssembly(DomainModelAssembly)
            .ShouldNot()
            .HaveDependencyOn("Application.Services")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            "Domain.Model must not depend on Application.Services");
    }

    [TestMethod]
    public void Domain_ShouldNotReference_EntityFramework()
    {
        var result = Types.InAssembly(DomainModelAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            "Domain.Model must not depend on EntityFrameworkCore directly");
    }

    [TestMethod]
    public void DomainShared_ShouldNotReference_AnyExternalLayer()
    {
        var result = Types.InAssembly(DomainSharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Domain.Model",
                "Application.Services",
                "Application.Contracts",
                "Infrastructure.Data",
                "Infrastructure.Repositories")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            "Domain.Shared must not depend on any other project layer");
    }

    [TestMethod]
    public void ApplicationContracts_ShouldNotReference_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationContractsAssembly)
            .ShouldNot()
            .HaveDependencyOn("Infrastructure.Data")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            "Application.Contracts must not depend on Infrastructure.Data");
    }

    [TestMethod]
    public void Api_ShouldNotReference_DomainModelDirectly()
    {
        // API endpoints should go through Application.Contracts.Services,
        // not manipulate domain entities directly.
        var apiAssembly = typeof(Program).Assembly;

        var result = Types.InAssembly(apiAssembly)
            .That()
            .ResideInNamespace("TaskFlow.Api.Endpoints")
            .ShouldNot()
            .HaveDependencyOn("Domain.Model")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            "API Endpoints must not depend on Domain.Model directly — use Application services");
    }

    [TestMethod]
    public void Api_ShouldNotReference_InfrastructureData()
    {
        var apiAssembly = typeof(Program).Assembly;

        var result = Types.InAssembly(apiAssembly)
            .That()
            .ResideInNamespace("TaskFlow.Api.Endpoints")
            .ShouldNot()
            .HaveDependencyOn("Infrastructure.Data")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            "API Endpoints must not depend on Infrastructure.Data directly");
    }
}
