using System.Net.Http.Json;

namespace TaskFlow.UI.Business.Services.Tags;

/// <summary>
/// Tag service — wraps Gateway HTTP calls.
/// </summary>
public partial class TagService : ITagService
{
    private readonly HttpClient _client;

    public TagService(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("TaskFlowGateway");
    }

    public async ValueTask<IImmutableList<TagSummary>> GetAll(CancellationToken ct)
    {
        var items = await _client.GetFromJsonAsync<List<TagApiDto>>("api/tags", ct);
        return items?.Select(MapToSummary).ToImmutableList() ?? ImmutableList<TagSummary>.Empty;
    }

    public async ValueTask<TagSummary?> GetById(Guid id, CancellationToken ct)
    {
        var item = await _client.GetFromJsonAsync<TagApiDto>($"api/tags/{id}", ct);
        return item is not null ? MapToSummary(item) : null;
    }

    public async ValueTask<TagSummary?> Create(TagSummary item, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync("api/tags", MapToApiDto(item), ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TagApiDto>(ct);
        return created is not null ? MapToSummary(created) : null;
    }

    public async ValueTask<TagSummary?> Update(TagSummary item, CancellationToken ct)
    {
        var response = await _client.PutAsJsonAsync($"api/tags/{item.Id}", MapToApiDto(item), ct);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TagApiDto>(ct);
        return updated is not null ? MapToSummary(updated) : null;
    }

    public async ValueTask<bool> Delete(Guid id, CancellationToken ct)
    {
        var response = await _client.DeleteAsync($"api/tags/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    private static TagSummary MapToSummary(TagApiDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name ?? string.Empty,
        Description = dto.Description,
    };

    private static TagApiDto MapToApiDto(TagSummary s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
    };

    private sealed class TagApiDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
