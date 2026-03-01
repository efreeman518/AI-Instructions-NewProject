using System.Net.Http.Json;

namespace TaskFlow.UI.Business.Services.TodoItems;

/// <summary>
/// TodoItem service — wraps Gateway HTTP calls, maps API DTOs to UI models at the service boundary.
/// </summary>
public partial class TodoItemService : ITodoItemService
{
    private readonly HttpClient _client;

    public TodoItemService(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("TaskFlowGateway");
    }

    public async ValueTask<IImmutableList<TodoItemSummary>> GetAll(CancellationToken ct)
    {
        var items = await _client.GetFromJsonAsync<List<TodoItemApiDto>>("api/todoitems", ct);
        return items?.Select(MapToSummary).ToImmutableList() ?? ImmutableList<TodoItemSummary>.Empty;
    }

    public async ValueTask<TodoItemSummary?> GetById(Guid id, CancellationToken ct)
    {
        var item = await _client.GetFromJsonAsync<TodoItemApiDto>($"api/todoitems/{id}", ct);
        return item is not null ? MapToSummary(item) : null;
    }

    public async ValueTask<TodoItemSummary?> Create(TodoItemSummary item, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync("api/todoitems", MapToApiDto(item), ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TodoItemApiDto>(ct);
        return created is not null ? MapToSummary(created) : null;
    }

    public async ValueTask<TodoItemSummary?> Update(TodoItemSummary item, CancellationToken ct)
    {
        var response = await _client.PutAsJsonAsync($"api/todoitems/{item.Id}", MapToApiDto(item), ct);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TodoItemApiDto>(ct);
        return updated is not null ? MapToSummary(updated) : null;
    }

    public async ValueTask<bool> Delete(Guid id, CancellationToken ct)
    {
        var response = await _client.DeleteAsync($"api/todoitems/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    public async ValueTask<IImmutableList<TodoItemSummary>> Search(string? searchTerm, CancellationToken ct)
    {
        var url = string.IsNullOrWhiteSpace(searchTerm)
            ? "api/todoitems"
            : $"api/todoitems?searchTerm={Uri.EscapeDataString(searchTerm)}";
        var items = await _client.GetFromJsonAsync<List<TodoItemApiDto>>(url, ct);
        return items?.Select(MapToSummary).ToImmutableList() ?? ImmutableList<TodoItemSummary>.Empty;
    }

    public async ValueTask<TodoItemSummary?> Start(Guid id, CancellationToken ct) =>
        await PostAction($"api/todoitems/{id}/start", ct);

    public async ValueTask<TodoItemSummary?> Complete(Guid id, CancellationToken ct) =>
        await PostAction($"api/todoitems/{id}/complete", ct);

    public async ValueTask<TodoItemSummary?> Block(Guid id, CancellationToken ct) =>
        await PostAction($"api/todoitems/{id}/block", ct);

    public async ValueTask<TodoItemSummary?> Unblock(Guid id, CancellationToken ct) =>
        await PostAction($"api/todoitems/{id}/unblock", ct);

    public async ValueTask<TodoItemSummary?> Cancel(Guid id, CancellationToken ct) =>
        await PostAction($"api/todoitems/{id}/cancel", ct);

    public async ValueTask<TodoItemSummary?> Archive(Guid id, CancellationToken ct) =>
        await PostAction($"api/todoitems/{id}/archive", ct);

    private async ValueTask<TodoItemSummary?> PostAction(string url, CancellationToken ct)
    {
        var response = await _client.PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<TodoItemApiDto>(ct);
        return item is not null ? MapToSummary(item) : null;
    }

    private static TodoItemSummary MapToSummary(TodoItemApiDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title ?? string.Empty,
        Description = dto.Description,
        Priority = dto.Priority,
        Status = dto.Status,
        EstimatedHours = dto.EstimatedHours,
        ActualHours = dto.ActualHours,
        StartDate = dto.StartDate,
        DueDate = dto.DueDate,
        CategoryId = dto.CategoryId,
        AssignedToId = dto.AssignedToId,
        TeamId = dto.TeamId,
        CategoryName = dto.CategoryName,
        AssignedToName = dto.AssignedToName,
        TeamName = dto.TeamName,
        CommentCount = dto.CommentCount,
        TagCount = dto.TagCount,
    };

    private static TodoItemApiDto MapToApiDto(TodoItemSummary s) => new()
    {
        Id = s.Id,
        Title = s.Title,
        Description = s.Description,
        Priority = s.Priority,
        Status = s.Status,
        EstimatedHours = s.EstimatedHours,
        ActualHours = s.ActualHours,
        StartDate = s.StartDate,
        DueDate = s.DueDate,
        CategoryId = s.CategoryId,
        AssignedToId = s.AssignedToId,
        TeamId = s.TeamId,
    };

    /// <summary>
    /// Lightweight API DTO — matches the shape returned by the Gateway/API.
    /// Keeps the UI decoupled from the backend Application.Models assembly.
    /// </summary>
    private sealed class TodoItemApiDto
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; }
        public Domain.Shared.TodoItemStatus Status { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? DueDate { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? AssignedToId { get; set; }
        public Guid? TeamId { get; set; }
        public string? CategoryName { get; set; }
        public string? AssignedToName { get; set; }
        public string? TeamName { get; set; }
        public int CommentCount { get; set; }
        public int TagCount { get; set; }
    }
}
