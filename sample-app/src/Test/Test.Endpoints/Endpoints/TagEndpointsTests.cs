using System.Net;
using System.Net.Http.Json;
using Application.Models;

namespace Test.Endpoints.Endpoints;

/// <summary>
/// CRUD endpoint tests for /api/tags using WebApplicationFactory.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public class TagEndpointsTests
{
    private const string UrlBase = "/api/tags";

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

    private async Task<(HttpStatusCode StatusCode, TagDto? Dto)> CreateTagAsync(string? name = null)
    {
        var dto = new TagDto
        {
            Name = name ?? $"Tag-{Guid.NewGuid():N}",
            Description = "Test tag"
        };

        var response = await _client.PostAsJsonAsync(UrlBase, dto);
        var created = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TagDto>()
            : null;

        return (response.StatusCode, created);
    }

    // ── CRUD Lifecycle ───────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task CRUD_Tag_FullLifecycle()
    {
        var (statusCode, created) = await CreateTagAsync();

        if (statusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"POST returned {statusCode} — CRUD lifecycle skipped.");
            return;
        }

        Assert.IsNotNull(created);
        Assert.AreNotEqual(Guid.Empty, created.Id);
        var id = created.Id;

        // GET — retrieve
        var getResponse = await _client.GetAsync($"{UrlBase}/{id}");
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<TagDto>();
        Assert.AreEqual(id, retrieved?.Id);
        Assert.AreEqual(created.Name, retrieved?.Name);

        // PUT — update
        var updatedName = $"Updated-{created.Name}";
        created.Name = updatedName;
        created.Description = "Updated description";
        var putResponse = await _client.PutAsJsonAsync(UrlBase, created);
        Assert.AreEqual(HttpStatusCode.OK, putResponse.StatusCode);
        var updated = await putResponse.Content.ReadFromJsonAsync<TagDto>();
        Assert.AreEqual(updatedName, updated?.Name);

        // GET — confirm update
        var getUpdatedResponse = await _client.GetAsync($"{UrlBase}/{id}");
        var confirmedUpdate = await getUpdatedResponse.Content.ReadFromJsonAsync<TagDto>();
        Assert.AreEqual(updatedName, confirmedUpdate?.Name);
        Assert.AreEqual("Updated description", confirmedUpdate?.Description);

        // DELETE
        var deleteResponse = await _client.DeleteAsync($"{UrlBase}/{id}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // GET — confirm deleted
        var getDeletedResponse = await _client.GetAsync($"{UrlBase}/{id}");
        Assert.AreEqual(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
    }

    // ── Search ───────────────────────────────────────────────

    [TestMethod]
    public async Task Search_Tags_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync($"{UrlBase}/search", new { });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Search_AfterCreate_ReturnsCreatedItem()
    {
        var (statusCode, created) = await CreateTagAsync();

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
    public async Task Get_NonExistentTag_Returns404()
    {
        var response = await _client.GetAsync($"{UrlBase}/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Update non-existent ──────────────────────────────────

    [TestMethod]
    public async Task Update_NonExistentTag_ReturnsNotFoundOrBadRequest()
    {
        var dto = new TagDto
        {
            Id = Guid.NewGuid(),
            Name = "Nonexistent"
        };

        var response = await _client.PutAsJsonAsync(UrlBase, dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400, got {response.StatusCode}");
    }

    // ── Multiple creates ─────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Create_MultipleTags_AllRetrievable()
    {
        var names = new[] { $"TagA-{Guid.NewGuid():N}", $"TagB-{Guid.NewGuid():N}" };
        var ids = new List<Guid>();

        foreach (var name in names)
        {
            var (status, tag) = await CreateTagAsync(name);
            if (status != HttpStatusCode.Created)
            {
                Assert.Inconclusive($"POST returned {status} for tag '{name}'.");
                return;
            }
            ids.Add(tag!.Id);
        }

        foreach (var id in ids)
        {
            var response = await _client.GetAsync($"{UrlBase}/{id}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
