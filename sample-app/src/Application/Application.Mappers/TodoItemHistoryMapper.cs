namespace Application.Mappers;

public static class TodoItemHistoryMapper
{
    public static TodoItemHistoryDto ToDto(this TodoItemHistory entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TodoItemId = entity.TodoItemId,
            Action = entity.Action,
            PreviousStatus = entity.PreviousStatus,
            NewStatus = entity.NewStatus,
            PreviousAssignedToId = entity.PreviousAssignedToId,
            NewAssignedToId = entity.NewAssignedToId,
            ChangeDescription = entity.ChangeDescription,
            ChangedBy = entity.ChangedBy,
            ChangedAt = entity.ChangedAt,
        };
}
