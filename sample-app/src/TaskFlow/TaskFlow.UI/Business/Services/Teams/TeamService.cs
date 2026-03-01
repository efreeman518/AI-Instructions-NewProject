using System.Net.Http.Json;
using Domain.Shared;

namespace TaskFlow.UI.Business.Services.Teams;

/// <summary>
/// Team service — wraps Gateway HTTP calls.
/// </summary>
public partial class TeamService : ITeamService
{
    private readonly HttpClient _client;

    public TeamService(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("TaskFlowGateway");
    }

    public async ValueTask<IImmutableList<TeamSummary>> GetAll(CancellationToken ct)
    {
        var items = await _client.GetFromJsonAsync<List<TeamApiDto>>("api/teams", ct);
        return items?.Select(MapToSummary).ToImmutableList() ?? ImmutableList<TeamSummary>.Empty;
    }

    public async ValueTask<TeamSummary?> GetById(Guid id, CancellationToken ct)
    {
        var item = await _client.GetFromJsonAsync<TeamApiDto>($"api/teams/{id}", ct);
        return item is not null ? MapToSummary(item) : null;
    }

    public async ValueTask<TeamSummary?> Create(TeamSummary item, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync("api/teams", MapToApiDto(item), ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TeamApiDto>(ct);
        return created is not null ? MapToSummary(created) : null;
    }

    public async ValueTask<TeamSummary?> Update(TeamSummary item, CancellationToken ct)
    {
        var response = await _client.PutAsJsonAsync($"api/teams/{item.Id}", MapToApiDto(item), ct);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TeamApiDto>(ct);
        return updated is not null ? MapToSummary(updated) : null;
    }

    public async ValueTask<bool> Delete(Guid id, CancellationToken ct)
    {
        var response = await _client.DeleteAsync($"api/teams/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    private static TeamSummary MapToSummary(TeamApiDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name ?? string.Empty,
        Description = dto.Description,
        IsActive = dto.IsActive,
        MemberCount = dto.Members?.Count ?? 0,
        Members = dto.Members?.Select(m => new TeamMemberSummary
        {
            Id = m.Id,
            UserId = m.UserId,
            DisplayName = m.DisplayName ?? string.Empty,
            Role = m.Role,
            JoinedAt = m.JoinedAt,
        }).ToImmutableList() ?? ImmutableList<TeamMemberSummary>.Empty,
    };

    private static TeamApiDto MapToApiDto(TeamSummary s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        IsActive = s.IsActive,
    };

    private sealed class TeamApiDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public List<TeamMemberApiDto>? Members { get; set; }
    }

    private sealed class TeamMemberApiDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? DisplayName { get; set; }
        public TeamMemberRole Role { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
    }
}
