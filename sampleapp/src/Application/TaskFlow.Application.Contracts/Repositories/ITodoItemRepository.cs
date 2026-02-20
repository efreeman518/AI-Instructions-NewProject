// ═══════════════════════════════════════════════════════════════
// Pattern: Repository interfaces split into QUERY and TRXN.
//   - IXxxRepositoryQuery: Read-only operations (injected into services for reads)
//   - IXxxRepositoryTrxn:  Write operations (injected into services for writes)
// Both live in Application.Contracts so they can be consumed by Application.Services
// without depending on Infrastructure.
// ═══════════════════════════════════════════════════════════════
// Pattern: Generic interfaces from Package.Infrastructure.Domain:
//   - IBaseRepositoryQuery<T, TDto> provides QueryPageProjectionAsync, QueryByIdProjectionAsync
//   - IBaseRepositoryTrxn<T> provides Add, Update, Delete
// Entity-specific repos extend these with custom query methods.
// ═══════════════════════════════════════════════════════════════

using Package.Infrastructure.Domain;
using Domain.Model.Entities;
using Application.Models.TodoItem;

namespace Application.Contracts.Repositories;

/// <summary>
/// Read operations for TodoItem.
/// Pattern: Inherits from IBaseRepositoryQuery for standard paged projection queries.
/// Adds entity-specific read methods (e.g., batch queries, hierarchy lookups).
/// </summary>
public interface ITodoItemRepositoryQuery : IBaseRepositoryQuery<TodoItem, TodoItemDto>
{
    /// <summary>
    /// Pattern: Custom query method — returns items by parent for hierarchy navigation.
    /// Uses projection internally for EF-safe SQL generation.
    /// </summary>
    Task<IReadOnlyList<TodoItemDto>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Optimized batch query — returns overdue items across all tenants.
    /// Used by the Scheduler/BackgroundService, not by API endpoints.
    /// </summary>
    Task<IReadOnlyList<TodoItemDto>> GetOverdueItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Pattern: Existence check — lightweight query returning bool.
    /// Used before delete operations for validation.
    /// </summary>
    Task<bool> HasActiveItemsInCategoryAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Count query for cross-entity rule validation.
    /// </summary>
    Task<int> CountActiveItemsForTeamAsync(Guid teamId, CancellationToken ct = default);
}

/// <summary>
/// Write operations for TodoItem.
/// Pattern: Inherits IBaseRepositoryTrxn for standard CRUD.
/// Adds entity-specific write methods (e.g., batch status updates).
/// </summary>
public interface ITodoItemRepositoryTrxn : IBaseRepositoryTrxn<TodoItem>
{
    /// <summary>
    /// Pattern: Batch update — sets status on multiple items at once.
    /// Bypasses individual entity tracking for performance.
    /// </summary>
    Task BatchUpdateStatusAsync(
        IEnumerable<Guid> todoItemIds,
        Domain.Model.Enums.TodoItemStatus newStatus,
        CancellationToken ct = default);
}
