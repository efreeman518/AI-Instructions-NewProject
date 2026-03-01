namespace Application.Models;

/// <summary>
/// Search filter for TodoItem queries.
/// </summary>
public record TodoItemSearchFilter
{
    public Guid? TenantId { get; set; }
    public string? SearchTerm { get; set; }
    public Domain.Shared.TodoItemStatus? Status { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? TeamId { get; set; }
    public int? MinPriority { get; set; }
    public int? MaxPriority { get; set; }
    public DateTimeOffset? DueBefore { get; set; }
    public DateTimeOffset? DueAfter { get; set; }
}
