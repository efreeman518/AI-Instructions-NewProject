namespace Application.MessageHandlers;

[ScopedMessageHandler]
public class TodoItemAssignedEventHandler(
    ILogger<TodoItemAssignedEventHandler> logger,
    ITodoItemRepositoryTrxn repoTrxn) : IMessageHandler<TodoItemAssignedEvent>
{
    public async Task HandleAsync(TodoItemAssignedEvent message, CancellationToken ct = default)
    {
        logger.LogInformation("Handling TodoItemAssignedEvent for {TodoItemId}", message.TodoItemId);

        var todoItem = await repoTrxn.GetAsync(message.TodoItemId, false, ct);
        if (todoItem == null)
        {
            logger.LogWarning("TodoItem {TodoItemId} not found for assigned event", message.TodoItemId);
            return;
        }

        var history = TodoItemHistory.Create(
            message.TenantId, message.TodoItemId,
            "AssignedTo", message.AssignedBy,
            previousAssignedToId: message.PreviousAssignedToId,
            newAssignedToId: message.NewAssignedToId,
            changeDescription: $"Todo item '{message.Title}' assignment changed");

        if (history.IsSuccess)
        {
            todoItem.History.Add(history.Value!);
            await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        }
    }
}
