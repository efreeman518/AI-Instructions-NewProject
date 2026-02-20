// ═══════════════════════════════════════════════════════════════
// Pattern: Endpoint integration tests — TodoItem full CRUD lifecycle.
// Demonstrates: POST→201, GET→200, PUT→200, DELETE→204, GET→404 flow.
// Uses CustomApiFactory with InMemory DB (default) or real SQL Server.
// Tests run sequentially within this class ([DoNotParallelize] on CRUD).
//
// Route prefix: /api/v1/tenant/{tenantId}/todoitems
// ═══════════════════════════════════════════════════════════════

using System.Net;
using System.Net.Http.Json;
using Application.Models.TodoItem;
using Domain.Model.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Package.Infrastructure.Common;

[assembly: Parallelize(Workers = 1, Scope = ExecutionScope.ClassLevel)]

namespace Test.Integration.Endpoints;

[TestClass]
public class TodoItemEndpointTests : EndpointTestBase
{
    private static HttpClient _httpClient = null!;
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Pattern: API versioned route with tenant segment.
    private static readonly string UrlBase = $"api/v1/tenant/{TestTenantId}/todoitems";

    [ClassInitialize]
    public static async Task ClassInit(TestContext testContext)
    {
        // Pattern: Initialize DB instance for integration tests.
        await ConfigureTestInstanceAsync(nameof(TodoItemEndpointTests));
        _httpClient = await GetHttpClient();
    }

    [ClassCleanup]
    public static async Task ClassCleanup() => await FactoryCleanup();

    // ═══════════════════════════════════════════════════════════════
    // CRUD Lifecycle — POST → GET → PUT → DELETE → GET(404)
    // Pattern: Sequential CRUD test verifying the full HTTP lifecycle.
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Integration")]
    [DoNotParallelize]
    public async Task CRUD_TodoItem_FullLifecycle()
    {
        // Pattern: Reset DB state before CRUD lifecycle test.
        await ResetDatabaseAsync(respawn: true);

        // ── POST (Create) ───────────────────────────────────────
        var createDto = new TodoItemDto
        {
            TenantId = TestTenantId,
            Title = $"Integration-Test-{Guid.NewGuid():N}",
            Description = "Created via endpoint integration test",
            Priority = 3,
            Status = TodoItemStatus.None
        };

        var createRequest = new DefaultRequest<TodoItemDto> { Item = createDto };
        var createResponse = await _httpClient.PostAsJsonAsync(UrlBase, createRequest);

        // Pattern: POST returns 201 Created with the created item in the response body.
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode,
            $"POST should return 201. Body: {await createResponse.Content.ReadAsStringAsync()}");

        var created = await createResponse.Content.ReadFromJsonAsync<DefaultResponse<TodoItemDto>>();
        Assert.IsNotNull(created?.Item, "Created response should contain the item.");
        var createdId = created.Item.Id;
        Assert.AreNotEqual(Guid.Empty, createdId, "Created item should have a non-empty ID.");

        // ── GET (Read) ──────────────────────────────────────────
        var getResponse = await _httpClient.GetAsync($"{UrlBase}/{createdId}");

        // Pattern: GET returns 200 OK with the item.
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode,
            "GET should return 200 for existing item.");

        var fetched = await getResponse.Content.ReadFromJsonAsync<DefaultResponse<TodoItemDto>>();
        Assert.IsNotNull(fetched?.Item);
        Assert.AreEqual(createDto.Title, fetched.Item.Title);
        Assert.AreEqual(TestTenantId, fetched.Item.TenantId);

        // ── PUT (Update) ────────────────────────────────────────
        var updateDto = fetched.Item;
        updateDto.Title = "Updated-Integration-Test";
        updateDto.Priority = 5;

        var updateRequest = new DefaultRequest<TodoItemDto> { Item = updateDto };
        var updateResponse = await _httpClient.PutAsJsonAsync($"{UrlBase}/{createdId}", updateRequest);

        // Pattern: PUT returns 200 OK with updated item.
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode,
            $"PUT should return 200. Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var updated = await updateResponse.Content.ReadFromJsonAsync<DefaultResponse<TodoItemDto>>();
        Assert.IsNotNull(updated?.Item);
        Assert.AreEqual("Updated-Integration-Test", updated.Item.Title);
        Assert.AreEqual(5, updated.Item.Priority);

        // ── DELETE ──────────────────────────────────────────────
        var deleteResponse = await _httpClient.DeleteAsync($"{UrlBase}/{createdId}");

        // Pattern: DELETE returns 204 NoContent.
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode,
            "DELETE should return 204.");

        // ── GET after DELETE (404) ──────────────────────────────
        var getAfterDeleteResponse = await _httpClient.GetAsync($"{UrlBase}/{createdId}");

        // Pattern: GET after DELETE returns 404 Not Found.
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
        // Arrange — Pattern: SearchRequest with filter and paging.
        var searchRequest = new SearchRequest<TodoItemSearchFilter>
        {
            Filter = new TodoItemSearchFilter { TenantId = TestTenantId },
            PageSize = 10,
            PageIndex = 1
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{UrlBase}/search", searchRequest);

        // Assert — Pattern: Search always returns 200, even with zero results.
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResponse<TodoItemDto>>();
        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalCount >= 0);
    }

    // ═══════════════════════════════════════════════════════════════
    // Validation — bad request scenarios
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Create_InvalidDto_ReturnsBadRequest()
    {
        // Arrange — Pattern: Missing required fields should trigger validation error.
        var invalidDto = new TodoItemDto
        {
            TenantId = TestTenantId,
            Title = "", // Title is required
            Priority = 0 // Priority 1-5
        };

        var request = new DefaultRequest<TodoItemDto> { Item = invalidDto };

        // Act
        var response = await _httpClient.PostAsJsonAsync(UrlBase, request);

        // Assert — Pattern: Validation failure returns 400 or 422 with ProblemDetails.
        Assert.IsTrue(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity,
            $"Invalid DTO should return 400/422. Got: {response.StatusCode}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Update_IdMismatch_ReturnsBadRequest()
    {
        // Arrange — URL id and body id differ.
        var urlId = Guid.NewGuid();
        var dto = new TodoItemDto
        {
            Id = Guid.NewGuid(), // Different from URL
            TenantId = TestTenantId,
            Title = "Mismatch Test",
            Priority = 3
        };

        var request = new DefaultRequest<TodoItemDto> { Item = dto };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"{UrlBase}/{urlId}", request);

        // Assert — Pattern: URL/body ID mismatch → 400 Bad Request.
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode,
            "URL/body ID mismatch should return 400.");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"{UrlBase}/{nonExistentId}");

        // Assert — Pattern: GET for non-existent ID returns 404.
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Delete_NonExistent_ReturnsNoContent()
    {
        // Arrange — Pattern: Idempotent delete — deleting non-existent item succeeds.
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _httpClient.DeleteAsync($"{UrlBase}/{nonExistentId}");

        // Assert — Pattern: Idempotent delete returns 204.
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode,
            "Idempotent DELETE for non-existent item should return 204.");
    }
}
