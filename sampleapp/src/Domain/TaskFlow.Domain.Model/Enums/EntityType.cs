// Pattern: Discriminator enum for polymorphic join pattern.
// Used by Attachment entity to link to any entity type via EntityId + EntityType.
// This avoids needing separate AttachmentForTodoItem, AttachmentForComment tables.

namespace Domain.Model.Enums;

public enum EntityType
{
    /// <summary>Attachment belongs to a TodoItem.</summary>
    TodoItem = 0,

    /// <summary>Attachment belongs to a Comment.</summary>
    Comment = 1
}
