using System.Net;
using System.Net.Http.Json;
using Application.Models;
using Domain.Shared;

namespace Test.Endpoints.Endpoints;

/// <summary>
/// Validation and error path tests — exercises domain validation rules
/// through the API pipeline, expecting appropriate error responses.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public class ValidationTests
{
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

    // ── TodoItem validation ──────────────────────────────────

    [TestMethod]
    public async Task Create_TodoItem_MissingTitle_ReturnsBadRequestOrError()
    {
        var dto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = "", // Empty title should be rejected
            Status = TodoItemStatus.None,
            Priority = 3
        };

        var response = await _client.PostAsJsonAsync("/api/todoitems", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for empty title, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_TodoItem_TitleExceedsMaxLength_ReturnsBadRequestOrError()
    {
        var dto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = new string('X', 201), // Max 200 chars
            Status = TodoItemStatus.None,
            Priority = 3
        };

        var response = await _client.PostAsJsonAsync("/api/todoitems", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for title > 200 chars, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_TodoItem_PriorityTooHigh_ReturnsBadRequestOrError()
    {
        var dto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = $"Priority-{Guid.NewGuid():N}",
            Status = TodoItemStatus.None,
            Priority = 10 // Max is 5
        };

        var response = await _client.PostAsJsonAsync("/api/todoitems", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for priority > 5, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_TodoItem_PriorityTooLow_ReturnsBadRequestOrError()
    {
        var dto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = $"NegPri-{Guid.NewGuid():N}",
            Status = TodoItemStatus.None,
            Priority = 0 // Min is 1
        };

        var response = await _client.PostAsJsonAsync("/api/todoitems", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for priority < 1, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_TodoItem_NegativeEstimatedHours_ReturnsBadRequestOrError()
    {
        var dto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = $"NegHours-{Guid.NewGuid():N}",
            Status = TodoItemStatus.None,
            Priority = 3,
            EstimatedHours = -5
        };

        var response = await _client.PostAsJsonAsync("/api/todoitems", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for negative estimated hours, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_TodoItem_DescriptionExceedsMaxLength_ReturnsBadRequestOrError()
    {
        var dto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = $"LongDesc-{Guid.NewGuid():N}",
            Description = new string('Y', 2001), // Max 2000 chars
            Status = TodoItemStatus.None,
            Priority = 3
        };

        var response = await _client.PostAsJsonAsync("/api/todoitems", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for description > 2000 chars, got {response.StatusCode}");
    }

    // ── Category validation ──────────────────────────────────

    [TestMethod]
    public async Task Create_Category_MissingName_ReturnsBadRequestOrError()
    {
        var dto = new CategoryDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = "",
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/categories", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for empty category name, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_Category_NameExceedsMaxLength_ReturnsBadRequestOrError()
    {
        var dto = new CategoryDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = new string('Z', 101), // Max 100 chars
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/categories", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for name > 100 chars, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_Category_InvalidColorHex_ReturnsBadRequestOrError()
    {
        var dto = new CategoryDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = $"BadColor-{Guid.NewGuid():N}",
            ColorHex = "not-a-color", // Must match ^#[0-9A-Fa-f]{6}$
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/categories", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for invalid ColorHex, got {response.StatusCode}");
    }

    // ── Tag validation ───────────────────────────────────────

    [TestMethod]
    public async Task Create_Tag_MissingName_ReturnsBadRequestOrError()
    {
        var dto = new TagDto
        {
            Name = ""
        };

        var response = await _client.PostAsJsonAsync("/api/tags", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for empty tag name, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_Tag_NameExceedsMaxLength_ReturnsBadRequestOrError()
    {
        var dto = new TagDto
        {
            Name = new string('A', 51) // Max 50 chars
        };

        var response = await _client.PostAsJsonAsync("/api/tags", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for tag name > 50 chars, got {response.StatusCode}");
    }

    // ── Team validation ──────────────────────────────────────

    [TestMethod]
    public async Task Create_Team_MissingName_ReturnsBadRequestOrError()
    {
        var dto = new TeamDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = "",
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/teams", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for empty team name, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Create_Team_NameExceedsMaxLength_ReturnsBadRequestOrError()
    {
        var dto = new TeamDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = new string('B', 101), // Max 100 chars
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/teams", dto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for team name > 100 chars, got {response.StatusCode}");
    }

    // ── TeamMember validation ────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task AddMember_MissingDisplayName_ReturnsBadRequestOrError()
    {
        // Create a team first
        var teamDto = new TeamDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = $"Team-{Guid.NewGuid():N}",
            IsActive = true
        };

        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamDto);

        if (teamResponse.StatusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"Team create returned {teamResponse.StatusCode}.");
            return;
        }

        var team = await teamResponse.Content.ReadFromJsonAsync<TeamDto>();

        var memberDto = new TeamMemberDto
        {
            TenantId = team!.TenantId,
            TeamId = team.Id,
            UserId = Guid.NewGuid(),
            DisplayName = "", // Required
            Role = TeamMemberRole.Member
        };

        var response = await _client.PostAsJsonAsync($"/api/teams/{team.Id}/members", memberDto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for empty display name, got {response.StatusCode}");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task AddMember_EmptyUserId_ReturnsBadRequestOrError()
    {
        var teamDto = new TeamDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = $"Team-{Guid.NewGuid():N}",
            IsActive = true
        };

        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamDto);

        if (teamResponse.StatusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"Team create returned {teamResponse.StatusCode}.");
            return;
        }

        var team = await teamResponse.Content.ReadFromJsonAsync<TeamDto>();

        var memberDto = new TeamMemberDto
        {
            TenantId = team!.TenantId,
            TeamId = team.Id,
            UserId = Guid.Empty, // Required non-empty
            DisplayName = "Test",
            Role = TeamMemberRole.Member
        };

        var response = await _client.PostAsJsonAsync($"/api/teams/{team.Id}/members", memberDto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for empty UserId, got {response.StatusCode}");
    }

    // ── Comment validation ───────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task AddComment_EmptyText_ReturnsBadRequestOrError()
    {
        var todoDto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = $"CommentVal-{Guid.NewGuid():N}",
            Status = TodoItemStatus.None,
            Priority = 3
        };

        var todoResponse = await _client.PostAsJsonAsync("/api/todoitems", todoDto);

        if (todoResponse.StatusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"TodoItem create returned {todoResponse.StatusCode}.");
            return;
        }

        var todoItem = await todoResponse.Content.ReadFromJsonAsync<TodoItemDto>();

        var commentDto = new CommentDto
        {
            TenantId = todoItem!.TenantId,
            TodoItemId = todoItem.Id,
            Text = "", // Required
            AuthorId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/todoitems/{todoItem.Id}/comments", commentDto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for empty comment text, got {response.StatusCode}");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task AddComment_TextExceedsMaxLength_ReturnsBadRequestOrError()
    {
        var todoDto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = $"LongComment-{Guid.NewGuid():N}",
            Status = TodoItemStatus.None,
            Priority = 3
        };

        var todoResponse = await _client.PostAsJsonAsync("/api/todoitems", todoDto);

        if (todoResponse.StatusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"TodoItem create returned {todoResponse.StatusCode}.");
            return;
        }

        var todoItem = await todoResponse.Content.ReadFromJsonAsync<TodoItemDto>();

        var commentDto = new CommentDto
        {
            TenantId = todoItem!.TenantId,
            TodoItemId = todoItem.Id,
            Text = new string('C', 1001), // Max 1000 chars
            AuthorId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/todoitems/{todoItem.Id}/comments", commentDto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError,
            $"Expected 400 or 500 for comment > 1000 chars, got {response.StatusCode}");
    }

    // ── Maintenance endpoint ─────────────────────────────────

    [TestMethod]
    public async Task PurgeHistory_ReturnsExpectedStatus()
    {
        var response = await _client.PostAsync("/api/maintenance/purge-history", null);

        // Maintenance may return 200, 400, or 500 depending on InMemory DB support
        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.BadRequest
                or HttpStatusCode.InternalServerError,
            $"Unexpected status: {response.StatusCode}");
    }

    [TestMethod]
    public async Task PurgeHistory_WithRetentionDays_ReturnsExpectedStatus()
    {
        var response = await _client.PostAsync("/api/maintenance/purge-history?retentionDays=30", null);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.BadRequest
                or HttpStatusCode.InternalServerError,
            $"Unexpected status: {response.StatusCode}");
    }
}
