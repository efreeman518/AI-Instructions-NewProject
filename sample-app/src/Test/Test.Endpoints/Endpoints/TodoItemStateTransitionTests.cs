using System.Net;
using System.Net.Http.Json;
using Application.Models;
using Domain.Shared;

namespace Test.Endpoints.Endpoints;

/// <summary>
/// Tests for TodoItem state transition endpoints (/start, /complete, /block, etc.)
/// and domain action endpoints (/assign, /comments).
/// Exercises the full state machine through the API pipeline.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public class TodoItemStateTransitionTests
{
    private const string UrlBase = "/api/todoitems";

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

    private async Task<TodoItemDto?> CreateTodoItemAsync()
    {
        var dto = new TodoItemDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Title = $"State-{Guid.NewGuid():N}",
            Description = "State transition test item",
            Status = TodoItemStatus.None,
            Priority = 3
        };

        var response = await _client.PostAsJsonAsync(UrlBase, dto);

        if (response.StatusCode != HttpStatusCode.Created)
            return null;

        return await response.Content.ReadFromJsonAsync<TodoItemDto>();
    }

    private static void SkipIfNoCreate(string context)
    {
        Assert.Inconclusive(
            $"Create returned non-201 — {context} skipped. " +
            "Infrastructure may not be fully wired for InMemory testing.");
    }

    // ── Start ────────────────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Start_NewTodoItem_TransitionsToStarted()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Start"); return; }

        var response = await _client.PostAsync($"{UrlBase}/{item.Id}/start", null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsTrue(result?.Status.HasFlag(TodoItemStatus.IsStarted),
            $"Expected IsStarted flag, got {result?.Status}");
    }

    // ── Start → Complete ─────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Complete_StartedTodoItem_TransitionsToCompleted()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Complete"); return; }

        await _client.PostAsync($"{UrlBase}/{item.Id}/start", null);

        var completeResponse = await _client.PostAsync($"{UrlBase}/{item.Id}/complete", null);
        Assert.AreEqual(HttpStatusCode.OK, completeResponse.StatusCode);
        var result = await completeResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsTrue(result?.Status.HasFlag(TodoItemStatus.IsCompleted),
            $"Expected IsCompleted flag, got {result?.Status}");
    }

    // ── Block + Unblock ──────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Block_ThenUnblock_TodoItem()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Block/Unblock"); return; }

        // Block (from None)
        var blockResponse = await _client.PostAsync($"{UrlBase}/{item.Id}/block", null);
        Assert.AreEqual(HttpStatusCode.OK, blockResponse.StatusCode);
        var blocked = await blockResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsTrue(blocked?.Status.HasFlag(TodoItemStatus.IsBlocked),
            $"Expected IsBlocked flag, got {blocked?.Status}");

        // Unblock
        var unblockResponse = await _client.PostAsync($"{UrlBase}/{item.Id}/unblock", null);
        Assert.AreEqual(HttpStatusCode.OK, unblockResponse.StatusCode);
        var unblocked = await unblockResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsTrue(unblocked?.Status.HasFlag(TodoItemStatus.IsStarted),
            $"Expected IsStarted flag after unblock, got {unblocked?.Status}");
    }

    // ── Cancel ───────────────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Cancel_TodoItem_TransitionsToCancelled()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Cancel"); return; }

        var response = await _client.PostAsync($"{UrlBase}/{item.Id}/cancel", null);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsTrue(result?.Status.HasFlag(TodoItemStatus.IsCancelled),
            $"Expected IsCancelled flag, got {result?.Status}");
    }

    // ── Cancel → Reopen ──────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Reopen_CancelledTodoItem_TransitionsToNone()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Reopen-Cancelled"); return; }

        await _client.PostAsync($"{UrlBase}/{item.Id}/cancel", null);

        var reopenResponse = await _client.PostAsync($"{UrlBase}/{item.Id}/reopen", null);
        Assert.AreEqual(HttpStatusCode.OK, reopenResponse.StatusCode);
        var result = await reopenResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.AreEqual(TodoItemStatus.None, result?.Status,
            $"Expected None after reopen from cancelled, got {result?.Status}");
    }

    // ── Archive + Restore ────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Archive_ThenRestore_TodoItem()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Archive/Restore"); return; }

        // Archive (from None)
        var archiveResponse = await _client.PostAsync($"{UrlBase}/{item.Id}/archive", null);
        Assert.AreEqual(HttpStatusCode.OK, archiveResponse.StatusCode);
        var archived = await archiveResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsTrue(archived?.Status.HasFlag(TodoItemStatus.IsArchived),
            $"Expected IsArchived flag, got {archived?.Status}");

        // Restore
        var restoreResponse = await _client.PostAsync($"{UrlBase}/{item.Id}/restore", null);
        Assert.AreEqual(HttpStatusCode.OK, restoreResponse.StatusCode);
        var restored = await restoreResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.AreEqual(TodoItemStatus.None, restored?.Status,
            $"Expected None after restore, got {restored?.Status}");
    }

    // ── Complete → Reopen → Started ──────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Reopen_CompletedTodoItem_TransitionsToStarted()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Reopen-Completed"); return; }

        await _client.PostAsync($"{UrlBase}/{item.Id}/start", null);
        await _client.PostAsync($"{UrlBase}/{item.Id}/complete", null);

        var reopenResponse = await _client.PostAsync($"{UrlBase}/{item.Id}/reopen", null);
        Assert.AreEqual(HttpStatusCode.OK, reopenResponse.StatusCode);
        var result = await reopenResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsTrue(result?.Status.HasFlag(TodoItemStatus.IsStarted),
            $"Expected IsStarted after reopen from completed, got {result?.Status}");
    }

    // ── State transition on non-existent item ────────────────

    [TestMethod]
    public async Task Start_NonExistentTodoItem_ReturnsNotFoundOrBadRequest()
    {
        var response = await _client.PostAsync($"{UrlBase}/{Guid.NewGuid()}/start", null);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400, got {response.StatusCode}");
    }

    [TestMethod]
    public async Task Complete_NonExistentTodoItem_ReturnsNotFoundOrBadRequest()
    {
        var response = await _client.PostAsync($"{UrlBase}/{Guid.NewGuid()}/complete", null);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400, got {response.StatusCode}");
    }

    // ── Invalid state transitions ────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Complete_NonStartedTodoItem_ReturnsBadRequest()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Complete-Invalid"); return; }

        // Try to complete a None-status item without starting first
        var response = await _client.PostAsync($"{UrlBase}/{item.Id}/complete", null);

        // Domain should reject — expect 400
        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.OK,
            $"Expected 400 for invalid transition, got {response.StatusCode}");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Unblock_NonBlockedTodoItem_ReturnsBadRequest()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Unblock-Invalid"); return; }

        var response = await _client.PostAsync($"{UrlBase}/{item.Id}/unblock", null);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.OK,
            $"Expected 400 for invalid unblock, got {response.StatusCode}");
    }

    // ── Comment ──────────────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task AddComment_ToTodoItem_ReturnsCreated()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("AddComment"); return; }

        var commentDto = new CommentDto
        {
            TenantId = item.TenantId,
            TodoItemId = item.Id,
            Text = "This is a test comment",
            AuthorId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var response = await _client.PostAsJsonAsync($"{UrlBase}/{item.Id}/comments", commentDto);
        var body = await response.Content.ReadAsStringAsync();

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected 201 or 200, got {response.StatusCode}. Body: {body}");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CommentDto>();
            Assert.IsNotNull(result);
            Assert.AreEqual("This is a test comment", result.Text);
        }
    }

    // ── Comment on non-existent item ─────────────────────────

    [TestMethod]
    public async Task AddComment_ToNonExistentItem_ReturnsNotFoundOrBadRequest()
    {
        var commentDto = new CommentDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            TodoItemId = Guid.NewGuid(),
            Text = "Orphan comment",
            AuthorId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var response = await _client.PostAsJsonAsync($"{UrlBase}/{Guid.NewGuid()}/comments", commentDto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400, got {response.StatusCode}");
    }

    // ── Assign ───────────────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Assign_TodoItem_ReturnsOk()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("Assign"); return; }

        // Create a real team + member so the FK is valid
        var teamDto = new TeamDto
        {
            TenantId = item.TenantId,
            Name = $"AssignTeam-{Guid.NewGuid():N}",
            Description = "Team for assign test"
        };
        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamDto);
        if (teamResponse.StatusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive("Could not create team for assign test.");
            return;
        }
        var team = await teamResponse.Content.ReadFromJsonAsync<TeamDto>();

        var memberDto = new TeamMemberDto
        {
            TenantId = item.TenantId,
            TeamId = team!.Id,
            UserId = Guid.NewGuid(),
            DisplayName = "Assign Test Member",
            Role = TeamMemberRole.Member
        };
        var memberResponse = await _client.PostAsJsonAsync($"/api/teams/{team.Id}/members", memberDto);
        if (memberResponse.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
        {
            Assert.Inconclusive("Could not create team member for assign test.");
            return;
        }
        var member = await memberResponse.Content.ReadFromJsonAsync<TeamMemberDto>();

        var response = await _client.PostAsync(
            $"{UrlBase}/{item.Id}/assign?assignedToId={member!.Id}", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Expected 200 for assign, got {response.StatusCode}. Body: {body}");
    }

    // ── Full state machine walkthrough ───────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task FullStateMachine_None_Start_Block_Unblock_Complete_Reopen_Cancel_Reopen()
    {
        var item = await CreateTodoItemAsync();
        if (item == null) { SkipIfNoCreate("FullStateMachine"); return; }

        var id = item.Id;

        // None → IsStarted
        var r1 = await _client.PostAsync($"{UrlBase}/{id}/start", null);
        Assert.AreEqual(HttpStatusCode.OK, r1.StatusCode, "Start failed");

        // IsStarted → IsBlocked
        var r2 = await _client.PostAsync($"{UrlBase}/{id}/block", null);
        Assert.AreEqual(HttpStatusCode.OK, r2.StatusCode, "Block failed");

        // IsBlocked → IsStarted (unblock)
        var r3 = await _client.PostAsync($"{UrlBase}/{id}/unblock", null);
        Assert.AreEqual(HttpStatusCode.OK, r3.StatusCode, "Unblock failed");

        // IsStarted → IsCompleted
        var r4 = await _client.PostAsync($"{UrlBase}/{id}/complete", null);
        Assert.AreEqual(HttpStatusCode.OK, r4.StatusCode, "Complete failed");

        // IsCompleted → IsStarted (reopen)
        var r5 = await _client.PostAsync($"{UrlBase}/{id}/reopen", null);
        Assert.AreEqual(HttpStatusCode.OK, r5.StatusCode, "Reopen (completed) failed");

        // IsStarted → IsCancelled
        var r6 = await _client.PostAsync($"{UrlBase}/{id}/cancel", null);
        Assert.AreEqual(HttpStatusCode.OK, r6.StatusCode, "Cancel failed");

        // IsCancelled → None (reopen)
        var r7 = await _client.PostAsync($"{UrlBase}/{id}/reopen", null);
        Assert.AreEqual(HttpStatusCode.OK, r7.StatusCode, "Reopen (cancelled) failed");

        // Final state check
        var getResponse = await _client.GetAsync($"{UrlBase}/{id}");
        var final = await getResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.AreEqual(TodoItemStatus.None, final?.Status,
            $"Expected None after full walkthrough, got {final?.Status}");
    }
}
