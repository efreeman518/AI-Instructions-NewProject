using System.Net.Http.Json;

namespace TaskFlow.UI.Business.Services.Categories;

/// <summary>
/// Category service — wraps Gateway HTTP calls.
/// </summary>
public partial class CategoryService : ICategoryService
{
    private readonly HttpClient _client;

    public CategoryService(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("TaskFlowGateway");
    }

    public async ValueTask<IImmutableList<CategorySummary>> GetAll(CancellationToken ct)
    {
        var items = await _client.GetFromJsonAsync<List<CategoryApiDto>>("api/categories", ct);
        return items?.Select(MapToSummary).ToImmutableList() ?? ImmutableList<CategorySummary>.Empty;
    }

    public async ValueTask<CategorySummary?> GetById(Guid id, CancellationToken ct)
    {
        var item = await _client.GetFromJsonAsync<CategoryApiDto>($"api/categories/{id}", ct);
        return item is not null ? MapToSummary(item) : null;
    }

    public async ValueTask<CategorySummary?> Create(CategorySummary item, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync("api/categories", MapToApiDto(item), ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CategoryApiDto>(ct);
        return created is not null ? MapToSummary(created) : null;
    }

    public async ValueTask<CategorySummary?> Update(CategorySummary item, CancellationToken ct)
    {
        var response = await _client.PutAsJsonAsync($"api/categories/{item.Id}", MapToApiDto(item), ct);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<CategoryApiDto>(ct);
        return updated is not null ? MapToSummary(updated) : null;
    }

    public async ValueTask<bool> Delete(Guid id, CancellationToken ct)
    {
        var response = await _client.DeleteAsync($"api/categories/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    private static CategorySummary MapToSummary(CategoryApiDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name ?? string.Empty,
        Description = dto.Description,
        ColorHex = dto.ColorHex,
        DisplayOrder = dto.DisplayOrder,
        IsActive = dto.IsActive,
    };

    private static CategoryApiDto MapToApiDto(CategorySummary s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        ColorHex = s.ColorHex,
        DisplayOrder = s.DisplayOrder,
        IsActive = s.IsActive,
    };

    private sealed class CategoryApiDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ColorHex { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
