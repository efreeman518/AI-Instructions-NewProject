using Domain.Shared;

namespace Application.Contracts.Events;

public record TodoItemUpdatedEvent(
    Guid TodoItemId,
    Guid TenantId,
    string Title,
    TodoItemStatus? PreviousStatus,
    TodoItemStatus? NewStatus,
    Guid? PreviousAssignedToId,
    Guid? NewAssignedToId,
    Guid UpdatedBy) : IMessage;
