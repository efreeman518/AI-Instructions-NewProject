namespace Application.MessageHandlers;

[ScopedMessageHandler]
public class TodoItemUpdatedEventHandler(
    ILogger<TodoItemUpdatedEventHandler> logger,
    ITodoItemRepositoryTrxn repoTrxn) : IMessageHandler<TodoItemUpdatedEvent>
{
    public async Task HandleAsync(TodoItemUpdatedEvent message, CancellationToken ct = default)
    {
        logger.LogInformation("Handling TodoItemUpdatedEvent for {TodoItemId}", message.TodoItemId);

        var todoItem = await repoTrxn.GetAsync(message.TodoItemId, false, ct);
        if (todoItem == null)
        {
            logger.LogWarning("TodoItem {TodoItemId} not found for updated event", message.TodoItemId);
            return;
        }

        var history = TodoItemHistory.Create(
            message.TenantId, message.TodoItemId,
            "Updated", message.UpdatedBy,
            previousStatus: message.PreviousStatus,
            newStatus: message.NewStatus,
            changeDescription: $"Todo item '{message.Title}' updated");

        if (history.IsSuccess)
        {
            todoItem.History.Add(history.Value!);
            await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        }
    }
}
