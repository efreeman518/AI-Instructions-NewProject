using System.Net;
using System.Net.Http.Json;
using Application.Models;

namespace Test.Integration;

/// <summary>
/// Integration tests that verify API endpoint contracts through HTTP.
/// Uses SharedTestFactory for TestContainer database — write-through tests
/// exercise the full bootstrapper pipeline including AuditInterceptor.
/// </summary>
[TestClass]
public class EndpointContractTests
{
    private HttpClient _client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _client = SharedTestFactory.CreateClient();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _client?.Dispose();
    }

    // --- Health ---

    [TestMethod]
    public async Task HealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // --- TodoItems ---

    [TestMethod]
    public async Task SearchTodoItems_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/todoitems/search",
            new { PageSize = 100, PageIndex = 1 });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetTodoItem_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/todoitems/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateTodoItem_Returns201()
    {
        var dto = new { Title = $"IntTest-{Guid.NewGuid()}", Description = "Integration test", Priority = 3 };

        var response = await _client.PostAsJsonAsync("/api/todoitems", dto);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Expected 201 Created but got {(int)response.StatusCode}. Body: {body}");
    }

    [TestMethod]
    public async Task CRUD_TodoItem_FullLifecycle()
    {
        // Create
        var dto = new { Title = $"CRUD-{Guid.NewGuid()}", Description = "Full lifecycle", Priority = 2 };
        var createResponse = await _client.PostAsJsonAsync("/api/todoitems", dto);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsNotNull(created);
        Assert.AreNotEqual(Guid.Empty, created.Id);

        // Read
        var getResponse = await _client.GetAsync($"/api/todoitems/{created.Id}");
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.AreEqual(dto.Title, fetched?.Title);

        // Update — PUT goes to root URL with ID in body (use the DTO directly like endpoint tests)
        created.Title += "-updated";
        created.Description = "Updated";
        created.Priority = 4;
        var updateResponse = await _client.PutAsJsonAsync("/api/todoitems", created);
        Assert.IsTrue(updateResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Update returned {(int)updateResponse.StatusCode}");

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/todoitems/{created.Id}");
        Assert.IsTrue(deleteResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Delete returned {(int)deleteResponse.StatusCode}");

        // Verify deleted
        var verifyResponse = await _client.GetAsync($"/api/todoitems/{created.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, verifyResponse.StatusCode);
    }

    // --- Categories ---

    [TestMethod]
    public async Task SearchCategories_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/categories/search",
            new { PageSize = 100, PageIndex = 1 });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetCategory_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/categories/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateCategory_Returns201()
    {
        var dto = new { Name = $"Cat-{Guid.NewGuid()}", Description = "Test category", ColorHex = "#FF5733", DisplayOrder = 1 };

        var response = await _client.PostAsJsonAsync("/api/categories", dto);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Expected 201 Created but got {(int)response.StatusCode}. Body: {body}");
    }

    // --- Tags ---

    [TestMethod]
    public async Task SearchTags_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/tags/search",
            new { PageSize = 100, PageIndex = 1 });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetTag_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/tags/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateTag_Returns201()
    {
        var dto = new { Name = $"Tag-{Guid.NewGuid()}", Description = "Test tag" };

        var response = await _client.PostAsJsonAsync("/api/tags", dto);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Expected 201 Created but got {(int)response.StatusCode}. Body: {body}");
    }

    // --- Teams ---

    [TestMethod]
    public async Task SearchTeams_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/teams/search",
            new { PageSize = 100, PageIndex = 1 });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetTeam_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/teams/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateTeam_Returns201()
    {
        var dto = new { Name = $"Team-{Guid.NewGuid()}", Description = "Test team" };

        var response = await _client.PostAsJsonAsync("/api/teams", dto);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Expected 201 Created but got {(int)response.StatusCode}. Body: {body}");
    }
}
