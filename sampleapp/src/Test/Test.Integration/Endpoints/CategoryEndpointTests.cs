// ═══════════════════════════════════════════════════════════════
// Pattern: Endpoint integration tests — Category CRUD lifecycle.
// Same CRUD pattern as TodoItemEndpointTests but for a simpler entity.
// Demonstrates cache-on-write behavior at the HTTP level.
//
// Route prefix: /api/v1/tenant/{tenantId}/categories
// ═══════════════════════════════════════════════════════════════

using System.Net;
using System.Net.Http.Json;
using Application.Models.Category;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EF.Common;

namespace Test.Integration.Endpoints;

[TestClass]
public class CategoryEndpointTests : EndpointTestBase
{
    private static HttpClient _httpClient = null!;
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Pattern: API versioned route with tenant segment.
    private static readonly string UrlBase = $"api/v1/tenant/{TestTenantId}/categories";

    [ClassInitialize]
    public static async Task ClassInit(TestContext testContext)
    {
        await ConfigureTestInstanceAsync(nameof(CategoryEndpointTests));
        _httpClient = await GetHttpClient();
    }

    [ClassCleanup]
    public static async Task ClassCleanup() => await FactoryCleanup();

    // ═══════════════════════════════════════════════════════════════
    // CRUD Lifecycle — POST → GET → PUT → DELETE → GET(404)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Integration")]
    [DoNotParallelize]
    public async Task CRUD_Category_FullLifecycle()
    {
        // Pattern: Reset DB state before CRUD lifecycle test.
        await ResetDatabaseAsync(respawn: true);

        // ── POST (Create) ───────────────────────────────────────
        var createDto = new CategoryDto
        {
            TenantId = TestTenantId,
            Name = $"Test-Category-{Guid.NewGuid():N}",
            Description = "Created via endpoint integration test",
            ColorHex = "#FF5733",
            DisplayOrder = 1,
            IsActive = true
        };

        var createRequest = new DefaultRequest<CategoryDto> { Item = createDto };
        var createResponse = await _httpClient.PostAsJsonAsync(UrlBase, createRequest);

        // Pattern: POST returns 201 Created.
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode,
            $"POST should return 201. Body: {await createResponse.Content.ReadAsStringAsync()}");

        var created = await createResponse.Content.ReadFromJsonAsync<DefaultResponse<CategoryDto>>();
        Assert.IsNotNull(created?.Item, "Created response should contain the item.");
        var createdId = created.Item.Id;
        Assert.AreNotEqual(Guid.Empty, createdId);

        // ── GET (Read) ──────────────────────────────────────────
        var getResponse = await _httpClient.GetAsync($"{UrlBase}/{createdId}");

        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode,
            "GET should return 200 for existing item.");

        var fetched = await getResponse.Content.ReadFromJsonAsync<DefaultResponse<CategoryDto>>();
        Assert.IsNotNull(fetched?.Item);
        Assert.AreEqual(createDto.Name, fetched.Item.Name);
        Assert.AreEqual("#FF5733", fetched.Item.ColorHex);

        // ── PUT (Update) ────────────────────────────────────────
        var updateDto = fetched.Item;
        updateDto.Name = "Updated-Category";
        updateDto.ColorHex = "#00FF00";
        updateDto.DisplayOrder = 2;

        var updateRequest = new DefaultRequest<CategoryDto> { Item = updateDto };
        var updateResponse = await _httpClient.PutAsJsonAsync($"{UrlBase}/{createdId}", updateRequest);

        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode,
            $"PUT should return 200. Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var updated = await updateResponse.Content.ReadFromJsonAsync<DefaultResponse<CategoryDto>>();
        Assert.IsNotNull(updated?.Item);
        Assert.AreEqual("Updated-Category", updated.Item.Name);
        Assert.AreEqual("#00FF00", updated.Item.ColorHex);

        // ── DELETE ──────────────────────────────────────────────
        var deleteResponse = await _httpClient.DeleteAsync($"{UrlBase}/{createdId}");

        // Pattern: DELETE returns 204 NoContent.
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode,
            "DELETE should return 204.");

        // ── GET after DELETE (404) ──────────────────────────────
        var getAfterDeleteResponse = await _httpClient.GetAsync($"{UrlBase}/{createdId}");

        Assert.AreEqual(HttpStatusCode.NotFound, getAfterDeleteResponse.StatusCode,
            "GET after DELETE should return 404.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Search — POST /search
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Search_ReturnsPagedResults()
    {
        // Arrange
        var searchRequest = new SearchRequest<CategorySearchFilter>
        {
            Filter = new CategorySearchFilter { TenantId = TestTenantId },
            PageSize = 10,
            PageIndex = 1
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{UrlBase}/search", searchRequest);

        // Assert — Pattern: Search always returns 200.
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResponse<CategoryDto>>();
        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalCount >= 0);
    }

    // ═══════════════════════════════════════════════════════════════
    // Validation scenarios
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        var response = await _httpClient.GetAsync($"{UrlBase}/{Guid.NewGuid()}");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Delete_NonExistent_ReturnsNoContent()
    {
        // Pattern: Idempotent delete.
        var response = await _httpClient.DeleteAsync($"{UrlBase}/{Guid.NewGuid()}");
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Update_IdMismatch_ReturnsBadRequest()
    {
        // Arrange — URL id and body id differ.
        var urlId = Guid.NewGuid();
        var dto = new CategoryDto
        {
            Id = Guid.NewGuid(), // Different from URL
            TenantId = TestTenantId,
            Name = "Mismatch Test"
        };

        var request = new DefaultRequest<CategoryDto> { Item = dto };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"{UrlBase}/{urlId}", request);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode,
            "URL/body ID mismatch should return 400.");
    }
}
