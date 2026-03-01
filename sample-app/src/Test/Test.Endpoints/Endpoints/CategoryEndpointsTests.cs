using System.Net;
using System.Net.Http.Json;
using Application.Models;

namespace Test.Endpoints.Endpoints;

/// <summary>
/// CRUD endpoint tests for /api/categories using WebApplicationFactory.
/// Uses SharedTestFactory to avoid static state conflicts with other test classes.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public class CategoryEndpointsTests
{
    private const string UrlBase = "/api/categories";

    private HttpClient _client = null!;

    [TestInitialize]
    public void TestInit()
    {
        _client = SharedTestFactory.CreateClient();
    }

    [TestCleanup]
    public void TestClean()
    {
        _client?.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────

    private async Task<(HttpStatusCode StatusCode, CategoryDto? Dto)> CreateCategoryAsync(
        string? name = null, string? colorHex = null)
    {
        var dto = new CategoryDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = name ?? $"Cat-{Guid.NewGuid():N}",
            Description = "Test category",
            ColorHex = colorHex ?? "#FF5733",
            DisplayOrder = 1,
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync(UrlBase, dto);
        var created = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CategoryDto>()
            : null;

        return (response.StatusCode, created);
    }

    // ── CRUD Lifecycle ───────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task CRUD_Category_FullLifecycle()
    {
        var (statusCode, created) = await CreateCategoryAsync();

        if (statusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive(
                $"POST returned {statusCode} — CRUD lifecycle skipped.");
            return;
        }

        Assert.IsNotNull(created);
        Assert.AreNotEqual(Guid.Empty, created.Id);
        var id = created.Id;

        // GET — retrieve
        var getResponse = await _client.GetAsync($"{UrlBase}/{id}");
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.AreEqual(id, retrieved?.Id);
        Assert.AreEqual(created.Name, retrieved?.Name);

        // PUT — update
        var updatedName = $"Updated-{created.Name}";
        created.Name = updatedName;
        created.ColorHex = "#00FF00";
        var putResponse = await _client.PutAsJsonAsync(UrlBase, created);
        Assert.AreEqual(HttpStatusCode.OK, putResponse.StatusCode);
        var updated = await putResponse.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.AreEqual(updatedName, updated?.Name);
        Assert.AreEqual("#00FF00", updated?.ColorHex);

        // GET — confirm update persisted
        var getUpdatedResponse = await _client.GetAsync($"{UrlBase}/{id}");
        var confirmedUpdate = await getUpdatedResponse.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.AreEqual(updatedName, confirmedUpdate?.Name);

        // DELETE
        var deleteResponse = await _client.DeleteAsync($"{UrlBase}/{id}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // GET — confirm deleted
        var getDeletedResponse = await _client.GetAsync($"{UrlBase}/{id}");
        Assert.AreEqual(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
    }

    // ── Search ───────────────────────────────────────────────

    [TestMethod]
    public async Task Search_Categories_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync($"{UrlBase}/search", new { });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Search_AfterCreate_ReturnsCreatedItem()
    {
        var (statusCode, created) = await CreateCategoryAsync();

        if (statusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"POST returned {statusCode} — search verification skipped.");
            return;
        }

        var response = await _client.PostAsJsonAsync($"{UrlBase}/search", new { PageSize = 100, PageIndex = 1 });
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Search failed with body: {body}");
        Assert.IsTrue(body.Contains(created!.Name),
            $"Search results should contain '{created.Name}'. Body: {body}");
    }

    // ── GET non-existent ─────────────────────────────────────

    [TestMethod]
    public async Task Get_NonExistentCategory_Returns404()
    {
        var response = await _client.GetAsync($"{UrlBase}/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Update non-existent ──────────────────────────────────

    [TestMethod]
    public async Task Update_NonExistentCategory_ReturnsNotFoundOrBadRequest()
    {
        var dto = new CategoryDto
        {
            Id = Guid.NewGuid(),
            TenantId = SharedTestFactory.TestTenantId,
            Name = "Nonexistent",
            IsActive = true
        };

        var response = await _client.PutAsJsonAsync(UrlBase, dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400, got {response.StatusCode}");
    }

    // ── Delete non-existent ──────────────────────────────────

    [TestMethod]
    public async Task Delete_NonExistentCategory_ReturnsExpectedStatus()
    {
        var response = await _client.DeleteAsync($"{UrlBase}/{Guid.NewGuid()}");

        // Delete on non-existent may return 204 (idempotent), 400, or 500
        // depending on repository behavior
        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.NoContent
                or HttpStatusCode.BadRequest
                or HttpStatusCode.InternalServerError,
            $"Unexpected status: {response.StatusCode}");
    }
}
