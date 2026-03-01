using System.Net;

namespace Test.Endpoints.Basic;

/// <summary>
/// Basic endpoint smoke tests — verifies health and static endpoints return expected status codes.
/// Modeled after https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Endpoints/Basic/BasicEndpointsTests.cs
/// </summary>
[TestClass]
[DoNotParallelize]
[TestCategory("Deterministic")]
public class BasicEndpointsTests : EndpointTestBase
{
    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await ConfigureTestInstanceAsync("BasicEndpoints");
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await BaseClassCleanup();
    }

    [TestInitialize]
    public void TestInit()
    {
        InitializeClient();
    }

    [TestCleanup]
    public void TestClean()
    {
        CleanupClient();
    }

    // ── Health endpoint ──────────────────────────────────────

    [TestMethod]
    public async Task HealthCheck_ReturnsOk()
    {
        var response = await Client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Static / content-type verification ───────────────────

    [TestMethod]
    [DataRow("/health", HttpStatusCode.OK)]
    public async Task Get_BasicEndpoint_ReturnsExpectedStatus(string url, HttpStatusCode expected)
    {
        var response = await Client.GetAsync(url);

        Assert.AreEqual(expected, response.StatusCode);
    }
}
