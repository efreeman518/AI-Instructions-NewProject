// Pattern: Read-only mapper — TodoItemHistory has no ToEntity() (it's event-driven).

using System.Linq.Expressions;
using Domain.Model.Entities;
using Application.Models.TodoItemHistory;

namespace Application.Contracts.Mappers;

public static class TodoItemHistoryMapper
{
    public static TodoItemHistoryDto ToDto(this TodoItemHistory entity) => new()
    {
        Id = entity.Id,
        TodoItemId = entity.TodoItemId,
        Action = entity.Action,
        PreviousStatus = entity.PreviousStatus,
        NewStatus = entity.NewStatus,
        ChangeDescription = entity.ChangeDescription,
        ChangedBy = entity.ChangedBy,
        ChangedAt = entity.ChangedAt
    };

    /// <summary>Search projector — used for paginated history listing.</summary>
    public static Expression<Func<TodoItemHistory, TodoItemHistoryDto>> ProjectorSearch =>
        entity => new TodoItemHistoryDto
        {
            Id = entity.Id,
            TodoItemId = entity.TodoItemId,
            Action = entity.Action,
            PreviousStatus = entity.PreviousStatus,
            NewStatus = entity.NewStatus,
            ChangeDescription = entity.ChangeDescription,
            ChangedBy = entity.ChangedBy,
            ChangedAt = entity.ChangedAt
        };
}
