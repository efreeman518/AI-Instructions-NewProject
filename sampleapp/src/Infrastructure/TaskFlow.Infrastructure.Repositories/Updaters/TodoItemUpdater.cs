// ═══════════════════════════════════════════════════════════════
// Pattern: Updater — static extension method on DbContext for child collection sync.
// Demonstrates: CollectionUtility.SyncCollectionWithResult for Comments and TodoItemTags,
// entity.Update() chained with Bind() for aggregated DomainResult,
// RelatedDeleteBehavior controlling cascade vs. unlink on removed children.
// ═══════════════════════════════════════════════════════════════

using Application.Models.TodoItem;
using Application.Models.Comment;
using Domain.Model.Entities;
using Domain.Model.ValueObjects;
using Package.Infrastructure.Common;
using Package.Infrastructure.Domain;

namespace Infrastructure.Repositories.Updaters;

/// <summary>
/// Pattern: Static updater class — extension methods on TaskFlowDbContextTrxn.
/// Handles synchronizing child collections (Comments, TodoItemTags) during TodoItem updates.
/// Called by TodoItemRepositoryTrxn.UpdateFromDto().
/// </summary>
internal static class TodoItemUpdater
{
    /// <summary>
    /// Pattern: UpdateFromDto — the primary write-path for TodoItem updates.
    /// 1. Calls entity.Update() to mutate the parent entity and validate.
    /// 2. Syncs Comments collection (create new, remove missing — append-only, no update).
    /// 3. Syncs TodoItemTags junction (create new, remove missing — no update needed).
    /// 4. Returns aggregated DomainResult with all validation errors combined.
    /// </summary>
    public static DomainResult<TodoItem> UpdateFromDto(
        this TaskFlowDbContextTrxn db,
        TodoItem entity,
        TodoItemDto dto,
        RelatedDeleteBehavior relatedDeleteBehavior = RelatedDeleteBehavior.Delete)
    {
        // ── Step 1: Update the parent entity ────────────────────
        // Pattern: Reconstruct owned value object from flat DTO properties.
        var schedule = new DateRange(dto.StartDate, dto.DueDate);

        var updateResult = entity.Update(
            title: dto.Title,
            description: dto.Description,
            status: dto.Status,
            priority: dto.Priority,
            estimatedHours: dto.EstimatedHours,
            actualHours: dto.ActualHours,
            categoryId: dto.CategoryId,
            assignedToId: dto.AssignedToId,
            parentId: dto.ParentId,
            schedule: schedule);

        if (!updateResult.IsSuccess)
            return updateResult;

        // ── Step 2: Sync Comments ───────────────────────────────
        // Pattern: Append-only child — only add new and remove missing.
        // Comments have no Update() method; existing comments are never modified.
        var commentErrors = SyncComments(db, entity, dto.Comments, relatedDeleteBehavior);

        // ── Step 3: Sync TodoItemTags (junction) ────────────────
        // Pattern: Junction entity sync — compare by TagId, add/remove as needed.
        var tagErrors = SyncTodoItemTags(db, entity, dto.Tags);

        // ── Step 4: Aggregate all errors ────────────────────────
        var allErrors = new List<string>();
        allErrors.AddRange(commentErrors);
        allErrors.AddRange(tagErrors);

        return allErrors.Count > 0
            ? DomainResult<TodoItem>.Failure(allErrors)
            : DomainResult<TodoItem>.Success(entity);
    }

    /// <summary>
    /// Pattern: Append-only child sync — Comments can only be created or deleted.
    /// Uses CollectionUtility.SyncCollectionWithResult for the generic diff algorithm.
    /// </summary>
    private static List<string> SyncComments(
        TaskFlowDbContextTrxn db,
        TodoItem entity,
        List<CommentDto> incomingComments,
        RelatedDeleteBehavior deleteBehavior)
    {
        var errors = new List<string>();

        CollectionUtility.SyncCollectionWithResult(
            existing: entity.Comments.ToList(),
            incoming: incomingComments,
            existingKey: e => e.Id,
            incomingKey: i => i.Id == Guid.Empty ? null : (Guid?)i.Id,
            // Pattern: Comments are append-only — update is a no-op.
            update: (existingComment, incomingDto) => { /* No update for append-only entities */ },
            // Pattern: Create new comment via entity's domain method.
            add: incomingDto =>
            {
                var result = entity.AddComment(incomingDto.Text, incomingDto.AuthorId.ToString());
                if (!result.IsSuccess)
                {
                    errors.AddRange(result.Errors);
                    return null;
                }
                return result.Value;
            },
            // Pattern: Remove based on RelatedDeleteBehavior.
            remove: existingComment =>
            {
                if (deleteBehavior == RelatedDeleteBehavior.None) return;
                entity.RemoveComment(existingComment.Id);
                // Pattern: If full delete, also remove from DbContext to generate DELETE SQL.
                if (deleteBehavior == RelatedDeleteBehavior.Delete)
                    db.Set<Comment>().Remove(existingComment);
            });

        return errors;
    }

    /// <summary>
    /// Pattern: Junction entity sync — Tags are linked via TodoItemTag.
    /// Incoming is a flat list of Tag GUIDs; existing is the junction collection.
    /// </summary>
    private static List<string> SyncTodoItemTags(
        TaskFlowDbContextTrxn db,
        TodoItem entity,
        List<Guid> incomingTagIds)
    {
        var errors = new List<string>();

        // Pattern: Convert flat GUID list to pseudo-DTOs for the sync algorithm.
        var existingTagIds = entity.TodoItemTags.Select(t => t.TagId).ToHashSet();
        var incomingSet = new HashSet<Guid>(incomingTagIds);

        // Remove tags no longer in the incoming set.
        var toRemove = entity.TodoItemTags.Where(t => !incomingSet.Contains(t.TagId)).ToList();
        foreach (var junction in toRemove)
        {
            entity.TodoItemTags.Remove(junction);
            db.Set<TodoItemTag>().Remove(junction);
        }

        // Add new tags not already present.
        var toAdd = incomingTagIds.Where(tagId => !existingTagIds.Contains(tagId));
        foreach (var tagId in toAdd)
        {
            var junction = TodoItemTag.Create(entity.Id, tagId);
            entity.TodoItemTags.Add(junction);
        }

        return errors;
    }
}
