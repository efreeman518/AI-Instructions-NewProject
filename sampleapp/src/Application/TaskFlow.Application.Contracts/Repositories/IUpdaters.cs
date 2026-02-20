// Pattern: Updater interface — a write-through service that wraps repository operations
// with additional logic (cache invalidation, event publishing, tenant boundary checks).
// Updaters live behind service methods and handle the "write pipeline":
//   1. Validate tenant boundary (via IRequestContext)
//   2. Execute the repository write
//   3. Invalidate relevant caches
//   4. Publish domain events via IInternalMessageBus

using Package.Infrastructure.Common;
using Domain.Model.Entities;
using Application.Models.TodoItem;

namespace Application.Contracts.Repositories;

/// <summary>
/// Pattern: Updater interface — orchestrates write operations for TodoItem.
/// Services call the Updater instead of calling IRepositoryTrxn directly.
/// This ensures consistent cache invalidation and event publishing.
/// </summary>
public interface ITodoItemUpdater
{
    Task<Result<TodoItemDto>> CreateAsync(TodoItemDto dto, CancellationToken ct = default);
    Task<Result<TodoItemDto>> UpdateAsync(TodoItemDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Updater for Category — includes cache-on-write pattern for static data.</summary>
public interface ICategoryUpdater
{
    Task<Result<Application.Models.Category.CategoryDto>> CreateAsync(
        Application.Models.Category.CategoryDto dto, CancellationToken ct = default);
    Task<Result<Application.Models.Category.CategoryDto>> UpdateAsync(
        Application.Models.Category.CategoryDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Updater for Tag — global entity (no tenant boundary check).</summary>
public interface ITagUpdater
{
    Task<Result<Application.Models.Tag.TagDto>> CreateAsync(
        Application.Models.Tag.TagDto dto, CancellationToken ct = default);
    Task<Result<Application.Models.Tag.TagDto>> UpdateAsync(
        Application.Models.Tag.TagDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Updater for Team — includes child member management.</summary>
public interface ITeamUpdater
{
    Task<Result<Application.Models.Team.TeamDto>> CreateAsync(
        Application.Models.Team.TeamDto dto, CancellationToken ct = default);
    Task<Result<Application.Models.Team.TeamDto>> UpdateAsync(
        Application.Models.Team.TeamDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result> AddMemberAsync(Guid teamId, Application.Models.Team.TeamMemberDto memberDto,
        CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid teamId, Guid memberId, CancellationToken ct = default);
}
