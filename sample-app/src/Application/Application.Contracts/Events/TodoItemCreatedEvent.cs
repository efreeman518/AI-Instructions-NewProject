namespace Application.Contracts.Events;

public record TodoItemCreatedEvent(
    Guid TodoItemId,
    Guid TenantId,
    string Title,
    Guid? AssignedToId,
    Guid CreatedBy) : IMessage;
