namespace TaskFlow.UI.Business.Services.TodoItems;

/// <summary>
/// UI-layer service interface for todo items — calls Gateway API.
/// </summary>
public interface ITodoItemService
{
    ValueTask<IImmutableList<TodoItemSummary>> GetAll(CancellationToken ct);
    ValueTask<TodoItemSummary?> GetById(Guid id, CancellationToken ct);
    ValueTask<TodoItemSummary?> Create(TodoItemSummary item, CancellationToken ct);
    ValueTask<TodoItemSummary?> Update(TodoItemSummary item, CancellationToken ct);
    ValueTask<bool> Delete(Guid id, CancellationToken ct);
    ValueTask<IImmutableList<TodoItemSummary>> Search(string? searchTerm, CancellationToken ct);

    // State transitions
    ValueTask<TodoItemSummary?> Start(Guid id, CancellationToken ct);
    ValueTask<TodoItemSummary?> Complete(Guid id, CancellationToken ct);
    ValueTask<TodoItemSummary?> Block(Guid id, CancellationToken ct);
    ValueTask<TodoItemSummary?> Unblock(Guid id, CancellationToken ct);
    ValueTask<TodoItemSummary?> Cancel(Guid id, CancellationToken ct);
    ValueTask<TodoItemSummary?> Archive(Guid id, CancellationToken ct);
}
