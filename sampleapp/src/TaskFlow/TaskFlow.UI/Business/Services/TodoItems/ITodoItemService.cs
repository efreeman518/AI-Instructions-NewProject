// ═══════════════════════════════════════════════════════════════
// Pattern: Client-side service interface — defines operations available
// to MVUX presentation models. Returns IImmutableList<T> for collections.
// The service layer is the only place that calls the Kiota API client.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI.Business.Services.TodoItems;

public interface ITodoItemService
{
    /// <summary>Get all todo items for the current tenant.</summary>
    ValueTask<IImmutableList<TodoItem>> GetAll(CancellationToken ct);

    /// <summary>Get a single todo item by ID.</summary>
    ValueTask<TodoItem> GetById(Guid id, CancellationToken ct);

    /// <summary>Create a new todo item.</summary>
    ValueTask Create(TodoItem item, CancellationToken ct);

    /// <summary>Update an existing todo item.</summary>
    ValueTask Update(TodoItem item, CancellationToken ct);

    /// <summary>Delete a todo item by ID.</summary>
    ValueTask Delete(Guid id, CancellationToken ct);

    /// <summary>Toggle completion status.</summary>
    ValueTask ToggleComplete(TodoItem item, CancellationToken ct);
}
