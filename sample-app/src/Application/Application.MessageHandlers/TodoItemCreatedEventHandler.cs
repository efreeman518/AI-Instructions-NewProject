namespace Application.MessageHandlers;

[ScopedMessageHandler]
public class TodoItemCreatedEventHandler(
    ILogger<TodoItemCreatedEventHandler> logger,
    ITodoItemRepositoryTrxn repoTrxn) : IMessageHandler<TodoItemCreatedEvent>
{
    public async Task HandleAsync(TodoItemCreatedEvent message, CancellationToken ct = default)
    {
        logger.LogInformation("Handling TodoItemCreatedEvent for {TodoItemId}", message.TodoItemId);

        var todoItem = await repoTrxn.GetAsync(message.TodoItemId, false, ct);
        if (todoItem == null)
        {
            logger.LogWarning("TodoItem {TodoItemId} not found for created event", message.TodoItemId);
            return;
        }

        var history = TodoItemHistory.Create(
            message.TenantId, message.TodoItemId,
            "Created", message.CreatedBy,
            changeDescription: $"Todo item '{message.Title}' created");

        if (history.IsSuccess)
        {
            todoItem.History.Add(history.Value!);
            await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        }
    }
}
