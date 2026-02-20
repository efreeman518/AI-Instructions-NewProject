// ═══════════════════════════════════════════════════════════════
// Pattern: Client-side service implementation — calls Gateway API via
// Kiota-generated client, maps wire DTOs to client records,
// and sends EntityMessage on mutations for MVUX auto-refresh.
//
// In a real project, TaskFlowApiClient is Kiota-generated from the
// Gateway OpenAPI spec. For this sample, we use contrived mock data
// to demonstrate the full pattern without a running Gateway.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.UI.Client;

namespace TaskFlow.UI.Business.Services.TodoItems;

/// <summary>
/// Pattern: Service implementation using primary constructor injection.
/// Calls the Kiota API client (which routes through Gateway).
/// Sends EntityMessage on mutations to trigger MVUX feed/state refresh.
/// </summary>
public class TodoItemService(
    TaskFlowApiClient api,
    IMessenger messenger) : ITodoItemService
{
    // Pattern: Contrived in-memory data for sample — in production,
    // these methods call the Kiota API client against the Gateway.
    private static readonly List<TodoItem> _mockItems =
    [
        new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
                Title = "Review pull request", Description = "Check the latest PR for TaskFlow",
                Priority = 3, CategoryName = "Development" },
        new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
                Title = "Write unit tests", Description = "Cover all domain entity patterns",
                Priority = 2, CategoryName = "Testing",
                DueDate = DateTimeOffset.UtcNow.AddDays(3) },
        new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
                Title = "Deploy to staging", Description = "Push latest build to staging environment",
                Priority = 4, CategoryName = "DevOps",
                DueDate = DateTimeOffset.UtcNow.AddDays(-1) },
    ];

    public async ValueTask<IImmutableList<TodoItem>> GetAll(CancellationToken ct)
    {
        // Pattern: In production → var data = await api.Api.TodoItems.GetAsync(cancellationToken: ct);
        //          return data?.Select(d => new TodoItem(d)).ToImmutableList() ?? [];
        await Task.Delay(100, ct); // Simulate network latency
        return _mockItems.ToImmutableList();
    }

    public async ValueTask<TodoItem> GetById(Guid id, CancellationToken ct)
    {
        // Pattern: In production → var data = await api.Api.TodoItems[id].GetAsync(cancellationToken: ct);
        //          return new TodoItem(data!);
        await Task.Delay(50, ct);
        return _mockItems.First(x => x.Id == id);
    }

    public async ValueTask Create(TodoItem item, CancellationToken ct)
    {
        // Pattern: In production → await api.Api.TodoItems.PostAsync(item.ToData(), cancellationToken: ct);
        var newItem = item with { Id = Guid.NewGuid() };
        _mockItems.Add(newItem);
        // Pattern: Broadcast mutation — MVUX models using .Observe() auto-refresh.
        messenger.Send(new EntityMessage<TodoItem>(EntityChange.Created, newItem));
    }

    public async ValueTask Update(TodoItem item, CancellationToken ct)
    {
        // Pattern: In production → await api.Api.TodoItems[item.Id].PutAsync(item.ToData(), cancellationToken: ct);
        var index = _mockItems.FindIndex(x => x.Id == item.Id);
        if (index >= 0) _mockItems[index] = item;
        messenger.Send(new EntityMessage<TodoItem>(EntityChange.Updated, item));
    }

    public async ValueTask Delete(Guid id, CancellationToken ct)
    {
        // Pattern: In production → await api.Api.TodoItems[id].DeleteAsync(cancellationToken: ct);
        _mockItems.RemoveAll(x => x.Id == id);
        messenger.Send(new EntityMessage<TodoItem>(EntityChange.Deleted, new TodoItem { Id = id }));
    }

    public async ValueTask ToggleComplete(TodoItem item, CancellationToken ct)
    {
        var updated = item with { IsCompleted = !item.IsCompleted };
        await Update(updated, ct);
    }
}
