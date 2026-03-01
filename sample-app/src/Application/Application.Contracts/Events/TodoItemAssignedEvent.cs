namespace Application.Contracts.Events;

public record TodoItemAssignedEvent(
    Guid TodoItemId,
    Guid TenantId,
    string Title,
    Guid? PreviousAssignedToId,
    Guid? NewAssignedToId,
    Guid AssignedBy) : IMessage;
