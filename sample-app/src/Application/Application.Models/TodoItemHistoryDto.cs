namespace Application.Models;

public record TodoItemHistoryDto : EntityBaseDto, ITenantEntityDto
{
    public Guid TenantId { get; set; }
    public Guid TodoItemId { get; set; }
    public string Action { get; set; } = null!;
    public TodoItemStatus? PreviousStatus { get; set; }
    public TodoItemStatus? NewStatus { get; set; }
    public Guid? PreviousAssignedToId { get; set; }
    public Guid? NewAssignedToId { get; set; }
    public string? ChangeDescription { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
