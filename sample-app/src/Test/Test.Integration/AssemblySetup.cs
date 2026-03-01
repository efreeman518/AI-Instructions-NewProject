namespace Test.Integration;

[TestClass]
public static class AssemblySetup
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext _)
    {
        await SharedTestFactory.InitializeAsync();
    }
}
