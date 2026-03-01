namespace Domain.Model;

public class TodoItemHistory : EntityBase, ITenantEntity<Guid>
{
    public static DomainResult<TodoItemHistory> Create(Guid tenantId, Guid todoItemId, string action, Guid changedBy,
        TodoItemStatus? previousStatus = null, TodoItemStatus? newStatus = null,
        Guid? previousAssignedToId = null, Guid? newAssignedToId = null, string? changeDescription = null)
    {
        var entity = new TodoItemHistory(tenantId, todoItemId, action, changedBy,
            previousStatus, newStatus, previousAssignedToId, newAssignedToId, changeDescription);
        return entity.Valid().Map(_ => entity);
    }

    private TodoItemHistory(Guid tenantId, Guid todoItemId, string action, Guid changedBy,
        TodoItemStatus? previousStatus, TodoItemStatus? newStatus,
        Guid? previousAssignedToId, Guid? newAssignedToId, string? changeDescription)
    {
        TenantId = tenantId;
        TodoItemId = todoItemId;
        Action = action;
        ChangedBy = changedBy;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        PreviousAssignedToId = previousAssignedToId;
        NewAssignedToId = newAssignedToId;
        ChangeDescription = changeDescription;
        ChangedAt = DateTimeOffset.UtcNow;
    }

    // EF-compatible constructor
    private TodoItemHistory() { }

    public Guid TenantId { get; init; }
    public Guid TodoItemId { get; private set; }
    public string Action { get; private set; } = null!;
    public TodoItemStatus? PreviousStatus { get; private set; }
    public TodoItemStatus? NewStatus { get; private set; }
    public Guid? PreviousAssignedToId { get; private set; }
    public Guid? NewAssignedToId { get; private set; }
    public string? ChangeDescription { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    // Navigation
    public TodoItem TodoItem { get; private set; } = null!;

    private DomainResult<TodoItemHistory> Valid()
    {
        var errors = new List<DomainError>();
        if (string.IsNullOrWhiteSpace(Action)) errors.Add(DomainError.Create("Action is required."));
        if (ChangedBy == Guid.Empty) errors.Add(DomainError.Create("ChangedBy is required."));
        return (errors.Count > 0)
            ? DomainResult<TodoItemHistory>.Failure(errors)
            : DomainResult<TodoItemHistory>.Success(this);
    }
}
