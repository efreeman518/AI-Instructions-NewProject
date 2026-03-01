using System.Net;
using System.Net.Http.Json;
using Application.Models;
using Domain.Shared;

namespace Test.Integration;

/// <summary>
/// Integration tests that verify multi-entity relationships through the API:
/// TodoItem + Category, TodoItem + Comments, TodoItem + Team assignment.
/// Exercises the full HTTP pipeline with TestContainer database.
/// </summary>
[TestClass]
public class RelationshipTests
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

    // ── Helpers ──────────────────────────────────────────────

    private async Task<TodoItemDto> CreateTodoItemAsync(Guid? categoryId = null)
    {
        var dto = new
        {
            Title = $"RelTest-{Guid.NewGuid():N}",
            Description = "Relationship test item",
            Priority = 3,
            CategoryId = categoryId
        };

        var response = await _client.PostAsJsonAsync("/api/todoitems", dto);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Failed to create TodoItem: {await response.Content.ReadAsStringAsync()}");
        var created = await response.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsNotNull(created);
        return created;
    }

    private async Task<CategoryDto> CreateCategoryAsync()
    {
        var dto = new
        {
            Name = $"Cat-{Guid.NewGuid():N}",
            Description = "Test category for relationships",
            ColorHex = "#4A90D9",
            DisplayOrder = 1
        };

        var response = await _client.PostAsJsonAsync("/api/categories", dto);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Failed to create Category: {await response.Content.ReadAsStringAsync()}");
        var created = await response.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.IsNotNull(created);
        return created;
    }

    private async Task<TagDto> CreateTagAsync()
    {
        var dto = new
        {
            Name = $"Tag-{Guid.NewGuid():N}",
            Description = "Test tag for relationships"
        };

        var response = await _client.PostAsJsonAsync("/api/tags", dto);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Failed to create Tag: {await response.Content.ReadAsStringAsync()}");
        var created = await response.Content.ReadFromJsonAsync<TagDto>();
        Assert.IsNotNull(created);
        return created;
    }

    private async Task<TeamDto> CreateTeamAsync()
    {
        var dto = new
        {
            Name = $"Team-{Guid.NewGuid():N}",
            Description = "Test team for relationships"
        };

        var response = await _client.PostAsJsonAsync("/api/teams", dto);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Failed to create Team: {await response.Content.ReadAsStringAsync()}");
        var created = await response.Content.ReadFromJsonAsync<TeamDto>();
        Assert.IsNotNull(created);
        return created;
    }

    private async Task<TeamMemberDto> AddTeamMemberAsync(Guid teamId)
    {
        var dto = new
        {
            TeamId = teamId,
            UserId = Guid.NewGuid(),
            DisplayName = $"Member-{Guid.NewGuid():N}",
            Role = 0 // TeamMemberRole.Member
        };

        var response = await _client.PostAsJsonAsync($"/api/teams/{teamId}/members", dto);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode,
            $"Failed to add TeamMember: {await response.Content.ReadAsStringAsync()}");
        var created = await response.Content.ReadFromJsonAsync<TeamMemberDto>();
        Assert.IsNotNull(created);
        return created;
    }

    // ── TodoItem + Category ─────────────────────────────────

    [TestMethod]
    public async Task TodoItem_WithCategory_CategoryPopulatedOnGet()
    {
        // Create a category
        var category = await CreateCategoryAsync();

        // Create a todo item with that category
        var todoItem = await CreateTodoItemAsync(categoryId: category.Id);

        // Fetch the todo item and verify category is populated
        var response = await _client.GetAsync($"/api/todoitems/{todoItem.Id}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsNotNull(fetched);
        Assert.AreEqual(category.Id, fetched.CategoryId);

        // Clean up
        await _client.DeleteAsync($"/api/todoitems/{todoItem.Id}");
        await _client.DeleteAsync($"/api/categories/{category.Id}");
    }

    [TestMethod]
    public async Task TodoItem_ChangeCategory_UpdateSucceeds()
    {
        // Create two categories
        var category1 = await CreateCategoryAsync();
        var category2 = await CreateCategoryAsync();

        // Create a todo item with category 1
        var todoItem = await CreateTodoItemAsync(categoryId: category1.Id);
        Assert.AreEqual(category1.Id, todoItem.CategoryId);

        // Update to category 2 (modify the DTO directly like endpoint tests)
        todoItem.CategoryId = category2.Id;
        var updateResponse = await _client.PutAsJsonAsync("/api/todoitems", todoItem);
        Assert.IsTrue(updateResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Update returned {(int)updateResponse.StatusCode}: {await updateResponse.Content.ReadAsStringAsync()}");

        // Verify
        var getResponse = await _client.GetAsync($"/api/todoitems/{todoItem.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.AreEqual(category2.Id, fetched?.CategoryId);

        // Clean up
        await _client.DeleteAsync($"/api/todoitems/{todoItem.Id}");
        await _client.DeleteAsync($"/api/categories/{category1.Id}");
        await _client.DeleteAsync($"/api/categories/{category2.Id}");
    }

    // ── TodoItem + Comments ─────────────────────────────────

    [TestMethod]
    public async Task TodoItem_AddComment_CommentAppearsOnGet()
    {
        // Create a todo item
        var todoItem = await CreateTodoItemAsync();

        // Add a comment
        var commentDto = new
        {
            TodoItemId = todoItem.Id,
            Text = "This is a test comment for relationship testing",
            AuthorId = Guid.NewGuid()
        };
        var commentResponse = await _client.PostAsJsonAsync($"/api/todoitems/{todoItem.Id}/comments", commentDto);
        Assert.AreEqual(HttpStatusCode.Created, commentResponse.StatusCode,
            $"Failed to add comment: {await commentResponse.Content.ReadAsStringAsync()}");

        // Fetch the todo item and verify comment is present
        var getResponse = await _client.GetAsync($"/api/todoitems/{todoItem.Id}");
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsNotNull(fetched);
        Assert.IsTrue(fetched.Comments.Count >= 1, "Expected at least 1 comment");
        Assert.IsTrue(fetched.Comments.Any(c => c.Text == commentDto.Text));

        // Clean up
        await _client.DeleteAsync($"/api/todoitems/{todoItem.Id}");
    }

    [TestMethod]
    public async Task TodoItem_AddMultipleComments_AllPresent()
    {
        var todoItem = await CreateTodoItemAsync();

        // Add 3 comments
        for (int i = 1; i <= 3; i++)
        {
            var commentDto = new
            {
                TodoItemId = todoItem.Id,
                Text = $"Comment #{i} — {Guid.NewGuid():N}",
                AuthorId = Guid.NewGuid()
            };
            var resp = await _client.PostAsJsonAsync($"/api/todoitems/{todoItem.Id}/comments", commentDto);
            Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        }

        // Verify all 3 comments appear
        var getResponse = await _client.GetAsync($"/api/todoitems/{todoItem.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsNotNull(fetched);
        Assert.AreEqual(3, fetched.Comments.Count, "Expected exactly 3 comments");

        // Clean up
        await _client.DeleteAsync($"/api/todoitems/{todoItem.Id}");
    }

    // ── TodoItem + Team Assignment ──────────────────────────

    [TestMethod]
    public async Task TodoItem_AssignToTeamMember_AssignmentPopulatedOnGet()
    {
        // Create team + member
        var team = await CreateTeamAsync();
        var member = await AddTeamMemberAsync(team.Id);

        // Create a todo item
        var todoItem = await CreateTodoItemAsync();

        // Assign to the team member
        var assignResponse = await _client.PostAsync(
            $"/api/todoitems/{todoItem.Id}/assign?assignedToId={member.Id}", null);
        Assert.IsTrue(assignResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Assign returned {(int)assignResponse.StatusCode}: {await assignResponse.Content.ReadAsStringAsync()}");

        // Fetch and verify assignment
        var getResponse = await _client.GetAsync($"/api/todoitems/{todoItem.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsNotNull(fetched);
        Assert.AreEqual(member.Id, fetched.AssignedToId);

        // Clean up
        await _client.DeleteAsync($"/api/todoitems/{todoItem.Id}");
        await _client.DeleteAsync($"/api/teams/{team.Id}/members/{member.Id}");
        await _client.DeleteAsync($"/api/teams/{team.Id}");
    }

    // ── Team + Members ──────────────────────────────────────

    [TestMethod]
    public async Task Team_AddMultipleMembers_AllRetrievable()
    {
        var team = await CreateTeamAsync();

        // Add 3 members
        var memberIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var member = await AddTeamMemberAsync(team.Id);
            memberIds.Add(member.Id);
        }

        // Fetch team and verify members
        var getResponse = await _client.GetAsync($"/api/teams/{team.Id}");
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<TeamDto>();
        Assert.IsNotNull(fetched);
        Assert.AreEqual(3, fetched.Members.Count, "Expected 3 team members");

        // Clean up
        foreach (var memberId in memberIds)
        {
            await _client.DeleteAsync($"/api/teams/{team.Id}/members/{memberId}");
        }
        await _client.DeleteAsync($"/api/teams/{team.Id}");
    }

    // ── Full Relationship Chain ──────────────────────────────

    [TestMethod]
    public async Task TodoItem_FullRelationshipChain_Category_Team_Comment()
    {
        // Create supporting entities
        var category = await CreateCategoryAsync();
        var team = await CreateTeamAsync();
        var member = await AddTeamMemberAsync(team.Id);

        // Create todo item with category
        var todoItem = await CreateTodoItemAsync(categoryId: category.Id);

        // Start the item (to show state transitions work with relationships)
        var startResponse = await _client.PostAsync($"/api/todoitems/{todoItem.Id}/start", null);
        Assert.IsTrue(startResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        // Assign to team member
        var assignResponse = await _client.PostAsync(
            $"/api/todoitems/{todoItem.Id}/assign?assignedToId={member.Id}", null);
        Assert.IsTrue(assignResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        // Add a comment
        var commentDto = new { TodoItemId = todoItem.Id, Text = "Working on this task", AuthorId = Guid.NewGuid() };
        var commentResponse = await _client.PostAsJsonAsync($"/api/todoitems/{todoItem.Id}/comments", commentDto);
        Assert.AreEqual(HttpStatusCode.Created, commentResponse.StatusCode);

        // Fetch and verify the full relationship chain
        var getResponse = await _client.GetAsync($"/api/todoitems/{todoItem.Id}");
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsNotNull(fetched);

        // Verify category
        Assert.AreEqual(category.Id, fetched.CategoryId);

        // Verify assignment
        Assert.AreEqual(member.Id, fetched.AssignedToId);

        // Verify comment
        Assert.IsTrue(fetched.Comments.Count >= 1);
        Assert.IsTrue(fetched.Comments.Any(c => c.Text == "Working on this task"));

        // Verify status is Started
        Assert.IsTrue(fetched.Status.HasFlag(TodoItemStatus.IsStarted));

        // Complete the item to verify state transitions work with all relationships wired
        var completeResponse = await _client.PostAsync($"/api/todoitems/{todoItem.Id}/complete", null);
        Assert.IsTrue(completeResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var finalGet = await _client.GetAsync($"/api/todoitems/{todoItem.Id}");
        var finalItem = await finalGet.Content.ReadFromJsonAsync<TodoItemDto>();
        Assert.IsTrue(finalItem?.Status.HasFlag(TodoItemStatus.IsCompleted) == true);

        // Clean up
        await _client.DeleteAsync($"/api/todoitems/{todoItem.Id}");
        await _client.DeleteAsync($"/api/teams/{team.Id}/members/{member.Id}");
        await _client.DeleteAsync($"/api/teams/{team.Id}");
        await _client.DeleteAsync($"/api/categories/{category.Id}");
    }
}
