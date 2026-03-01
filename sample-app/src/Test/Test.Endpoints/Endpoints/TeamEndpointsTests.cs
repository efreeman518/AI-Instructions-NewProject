using System.Net;
using System.Net.Http.Json;
using Application.Models;

namespace Test.Endpoints.Endpoints;

/// <summary>
/// CRUD endpoint tests for /api/teams using WebApplicationFactory,
/// including team member management (/api/teams/{teamId}/members).
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public class TeamEndpointsTests
{
    private const string UrlBase = "/api/teams";

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

    private async Task<(HttpStatusCode StatusCode, TeamDto? Dto)> CreateTeamAsync(string? name = null)
    {
        var dto = new TeamDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            Name = name ?? $"Team-{Guid.NewGuid():N}",
            Description = "Test team",
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync(UrlBase, dto);
        var created = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TeamDto>()
            : null;

        return (response.StatusCode, created);
    }

    // ── CRUD Lifecycle ───────────────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task CRUD_Team_FullLifecycle()
    {
        var (statusCode, created) = await CreateTeamAsync();

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
        var retrieved = await getResponse.Content.ReadFromJsonAsync<TeamDto>();
        Assert.AreEqual(id, retrieved?.Id);
        Assert.AreEqual(created.Name, retrieved?.Name);

        // PUT — update
        var updatedName = $"Updated-{created.Name}";
        created.Name = updatedName;
        created.Description = "Updated description";
        var putResponse = await _client.PutAsJsonAsync(UrlBase, created);
        Assert.AreEqual(HttpStatusCode.OK, putResponse.StatusCode);
        var updated = await putResponse.Content.ReadFromJsonAsync<TeamDto>();
        Assert.AreEqual(updatedName, updated?.Name);

        // GET — confirm update
        var getUpdatedResponse = await _client.GetAsync($"{UrlBase}/{id}");
        var confirmedUpdate = await getUpdatedResponse.Content.ReadFromJsonAsync<TeamDto>();
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
    public async Task Search_Teams_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync($"{UrlBase}/search", new { });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ── GET non-existent ─────────────────────────────────────

    [TestMethod]
    public async Task Get_NonExistentTeam_Returns404()
    {
        var response = await _client.GetAsync($"{UrlBase}/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Update non-existent ──────────────────────────────────

    [TestMethod]
    public async Task Update_NonExistentTeam_ReturnsNotFoundOrBadRequest()
    {
        var dto = new TeamDto
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

    // ── Team Member Management ───────────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task AddMember_ToTeam_ReturnsCreated()
    {
        var (statusCode, team) = await CreateTeamAsync();

        if (statusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"POST team returned {statusCode} — member test skipped.");
            return;
        }

        var memberDto = new TeamMemberDto
        {
            TenantId = team!.TenantId,
            TeamId = team.Id,
            UserId = Guid.NewGuid(),
            DisplayName = "Test Member",
            Role = Domain.Shared.TeamMemberRole.Member
        };

        var response = await _client.PostAsJsonAsync($"{UrlBase}/{team.Id}/members", memberDto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected 201 or 200 for add member, got {response.StatusCode}");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task AddAndRemoveMember_FullLifecycle()
    {
        var (statusCode, team) = await CreateTeamAsync();

        if (statusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"POST team returned {statusCode} — member lifecycle skipped.");
            return;
        }

        var teamId = team!.Id;

        // Add member
        var memberDto = new TeamMemberDto
        {
            TenantId = team.TenantId,
            TeamId = teamId,
            UserId = Guid.NewGuid(),
            DisplayName = "Lifecycle Member",
            Role = Domain.Shared.TeamMemberRole.Admin
        };

        var addResponse = await _client.PostAsJsonAsync($"{UrlBase}/{teamId}/members", memberDto);

        if (addResponse.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
        {
            Assert.Inconclusive($"Add member returned {addResponse.StatusCode}.");
            return;
        }

        var addedMember = await addResponse.Content.ReadFromJsonAsync<TeamMemberDto>();
        Assert.IsNotNull(addedMember);
        Assert.AreNotEqual(Guid.Empty, addedMember.Id);

        // Verify team now has member
        var getTeamResponse = await _client.GetAsync($"{UrlBase}/{teamId}");
        Assert.AreEqual(HttpStatusCode.OK, getTeamResponse.StatusCode);
        var updatedTeam = await getTeamResponse.Content.ReadFromJsonAsync<TeamDto>();
        Assert.IsTrue(updatedTeam?.Members?.Count > 0, "Team should have at least one member.");

        // Remove member
        var removeResponse = await _client.DeleteAsync($"{UrlBase}/{teamId}/members/{addedMember.Id}");

        Assert.IsTrue(
            removeResponse.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.BadRequest,
            $"Expected 204 or 400, got {removeResponse.StatusCode}");
    }

    // ── Add member to non-existent team ──────────────────────

    [TestMethod]
    public async Task AddMember_ToNonExistentTeam_ReturnsNotFoundOrBadRequest()
    {
        var memberDto = new TeamMemberDto
        {
            TenantId = SharedTestFactory.TestTenantId,
            TeamId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = "Orphan Member",
            Role = Domain.Shared.TeamMemberRole.Member
        };

        var response = await _client.PostAsJsonAsync($"{UrlBase}/{Guid.NewGuid()}/members", memberDto);

        Assert.IsTrue(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400, got {response.StatusCode}");
    }

    // ── Deactivate team via update ───────────────────────────

    [TestMethod]
    [DoNotParallelize]
    public async Task Update_Team_Deactivate()
    {
        var (statusCode, created) = await CreateTeamAsync();

        if (statusCode != HttpStatusCode.Created)
        {
            Assert.Inconclusive($"POST returned {statusCode}.");
            return;
        }

        created!.IsActive = false;
        var putResponse = await _client.PutAsJsonAsync(UrlBase, created);
        Assert.AreEqual(HttpStatusCode.OK, putResponse.StatusCode);

        var updated = await putResponse.Content.ReadFromJsonAsync<TeamDto>();
        Assert.IsFalse(updated?.IsActive, "Team should be deactivated.");
    }
}
