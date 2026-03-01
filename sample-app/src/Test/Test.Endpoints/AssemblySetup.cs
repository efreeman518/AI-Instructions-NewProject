namespace Test.Endpoints;

/// <summary>
/// Assembly-level lifecycle for endpoint tests.
/// Initializes SharedTestFactory (and TestContainer if configured) before any tests run.
/// </summary>
[TestClass]
public static class AssemblySetup
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext _)
    {
        await SharedTestFactory.InitializeAsync();
    }
}
