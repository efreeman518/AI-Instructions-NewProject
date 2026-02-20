// Pattern: Service interfaces for secondary entities.
// Simpler entities have simpler service interfaces.

using Package.Infrastructure.Common;
using Package.Infrastructure.Domain;
using Application.Models.Category;
using Application.Models.Tag;
using Application.Models.Team;
using Application.Models.Reminder;
using Application.Models.Attachment;

namespace Application.Contracts.Services;

// ═══════════════════════════════════════════════════════════════
// Category — CRUD + static data cache
// ═══════════════════════════════════════════════════════════════

public interface ICategoryService
{
    Task<Result<PagedResponse<CategoryDto>>> SearchAsync(CategorySearchFilter filter, CancellationToken ct = default);
    Task<Result<CategoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<CategoryDto>> CreateAsync(CategoryDto dto, CancellationToken ct = default);
    Task<Result<CategoryDto>> UpdateAsync(CategoryDto dto, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Delete with cross-entity rule — uses CategoryDeletionRule to prevent
    /// deleting categories that have active TodoItems assigned.
    /// </summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Static data method — returns all categories for a tenant, served from cache.
    /// The cache is populated on first read and invalidated on write (cache-on-write pattern).
    /// </summary>
    Task<Result<IReadOnlyList<CategoryDto>>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════════════
// Tag — non-tenant entity, simpler CRUD
// ═══════════════════════════════════════════════════════════════

public interface ITagService
{
    Task<Result<PagedResponse<TagDto>>> SearchAsync(TagSearchFilter filter, CancellationToken ct = default);
    Task<Result<TagDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<TagDto>> CreateAsync(TagDto dto, CancellationToken ct = default);
    Task<Result<TagDto>> UpdateAsync(TagDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Pattern: Global lookup — returns all tags (no tenant filter).</summary>
    Task<Result<IReadOnlyList<TagDto>>> GetAllAsync(CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════════════
// Team — CRUD + child member management
// ═══════════════════════════════════════════════════════════════

public interface ITeamService
{
    Task<Result<PagedResponse<TeamDto>>> SearchAsync(TeamSearchFilter filter, CancellationToken ct = default);
    Task<Result<TeamDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<TeamDto>> CreateAsync(TeamDto dto, CancellationToken ct = default);
    Task<Result<TeamDto>> UpdateAsync(TeamDto dto, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Deactivate with cross-entity rule — uses TeamDeactivationRule.
    /// </summary>
    Task<Result> DeactivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Pattern: Child entity management through parent service.</summary>
    Task<Result> AddMemberAsync(Guid teamId, TeamMemberDto memberDto, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid teamId, Guid memberId, CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════════════
// Reminder — CRUD + scheduler integration
// ═══════════════════════════════════════════════════════════════

public interface IReminderService
{
    Task<Result<IReadOnlyList<ReminderDto>>> GetByTodoItemIdAsync(Guid todoItemId, CancellationToken ct = default);
    Task<Result<ReminderDto>> CreateAsync(ReminderDto dto, CancellationToken ct = default);
    Task<Result> DeactivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Scheduler-facing method — called by TickerQ job.
    /// Returns due reminders and marks them as fired.
    /// </summary>
    Task<Result<IReadOnlyList<ReminderDto>>> ProcessDueRemindersAsync(CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════════════
// Attachment — polymorphic upload/download
// ═══════════════════════════════════════════════════════════════

public interface IAttachmentService
{
    Task<Result<IReadOnlyList<AttachmentDto>>> GetByEntityAsync(
        Guid entityId,
        Domain.Model.Enums.EntityType entityType,
        CancellationToken ct = default);

    /// <summary>
    /// Pattern: File upload — accepts stream, stores in blob, creates metadata record.
    /// </summary>
    Task<Result<AttachmentDto>> UploadAsync(
        Guid entityId,
        Domain.Model.Enums.EntityType entityType,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
