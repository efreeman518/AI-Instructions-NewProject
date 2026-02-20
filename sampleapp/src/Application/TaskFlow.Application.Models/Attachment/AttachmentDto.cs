// Pattern: Polymorphic entity DTO — Attachment.
// Uses EntityType discriminator to associate with different parent entity types.

using Domain.Model.Enums;

namespace Application.Models.Attachment;

public class AttachmentDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Pattern: Polymorphic FK — points to any entity's ID (TodoItem, Comment, etc.).</summary>
    public Guid EntityId { get; set; }

    /// <summary>Pattern: Discriminator — identifies which entity type this attachment belongs to.</summary>
    public EntityType EntityType { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    /// <summary>Pattern: Blob URI — reference to external storage (Azure Blob).</summary>
    public string BlobUri { get; set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}
