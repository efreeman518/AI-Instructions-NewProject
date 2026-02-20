// Pattern: Polymorphic join entity — links to ANY entity type via EntityId + EntityType discriminator.
// Avoids N separate attachment tables. The EntityType enum determines which entity the FK points to.
// This entity inherits tenant context from its parent (TodoItem or Comment).

using Domain.Model.Enums;
using Package.Infrastructure.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// File attachment that can belong to a TodoItem OR a Comment.
/// Uses polymorphic join: EntityId + EntityType discriminator.
/// The blob is stored externally (Azure Blob Storage); this entity holds the URI.
/// </summary>
public class Attachment : EntityBase, ITenantEntity<Guid>
{
    /// <summary>Tenant isolation — set from parent entity at creation.</summary>
    public Guid TenantId { get; init; }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Polymorphic join — EntityId + EntityType discriminator.
    // EntityId points to either a TodoItem.Id or a Comment.Id.
    // EntityType tells the system which table to resolve against.
    // No FK constraint in DB — validated at the application layer.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>The ID of the owning entity (TodoItem or Comment).</summary>
    public Guid EntityId { get; init; }

    /// <summary>Discriminator — which entity type owns this attachment.</summary>
    public EntityType EntityType { get; init; }

    /// <summary>Original filename as uploaded by the user.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>MIME type (e.g., "application/pdf", "image/png").</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; private set; }

    /// <summary>URI to the blob in external storage (Azure Blob Storage).</summary>
    public string BlobUri { get; private set; } = string.Empty;

    /// <summary>When the attachment was uploaded.</summary>
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>User who uploaded the attachment.</summary>
    public string UploadedBy { get; init; } = string.Empty;

    private Attachment() { }

    public static DomainResult<Attachment> Create(
        Guid tenantId,
        Guid entityId,
        EntityType entityType,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string blobUri,
        string uploadedBy)
    {
        var entity = new Attachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityId = entityId,
            EntityType = entityType,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            BlobUri = blobUri,
            UploadedBy = uploadedBy,
            UploadedAt = DateTimeOffset.UtcNow
        };

        return entity.Valid() ? DomainResult<Attachment>.Success(entity)
                              : DomainResult<Attachment>.Failure(entity._validationErrors);
    }

    // Pattern: No Update() — attachments are immutable. Delete and re-upload.

    private List<string> _validationErrors = [];

    private bool Valid()
    {
        _validationErrors = [];

        if (string.IsNullOrWhiteSpace(FileName))
            _validationErrors.Add("FileName is required.");

        if (FileName?.Length > 255)
            _validationErrors.Add("FileName must not exceed 255 characters.");

        if (string.IsNullOrWhiteSpace(ContentType))
            _validationErrors.Add("ContentType is required.");

        if (FileSizeBytes <= 0)
            _validationErrors.Add("FileSizeBytes must be greater than zero.");

        if (string.IsNullOrWhiteSpace(BlobUri))
            _validationErrors.Add("BlobUri is required.");

        return _validationErrors.Count == 0;
    }
}
