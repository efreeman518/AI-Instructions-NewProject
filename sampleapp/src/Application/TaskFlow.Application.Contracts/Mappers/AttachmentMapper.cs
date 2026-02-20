// Pattern: Mapper for polymorphic entity — Attachment uses EntityType discriminator.
// Immutable entity: no ToEntity() (attachments are created through infrastructure/blob services).

using System.Linq.Expressions;
using Domain.Model.Entities;
using Application.Models.Attachment;

namespace Application.Contracts.Mappers;

public static class AttachmentMapper
{
    public static AttachmentDto ToDto(this Attachment entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        EntityId = entity.EntityId,
        EntityType = entity.EntityType,
        FileName = entity.FileName,
        ContentType = entity.ContentType,
        FileSizeBytes = entity.FileSizeBytes,
        BlobUri = entity.BlobUri,
        UploadedAt = entity.UploadedAt,
        UploadedBy = entity.UploadedBy
    };

    /// <summary>Search projector — lists attachments for a given entity.</summary>
    public static Expression<Func<Attachment, AttachmentDto>> ProjectorSearch =>
        entity => new AttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EntityId = entity.EntityId,
            EntityType = entity.EntityType,
            FileName = entity.FileName,
            ContentType = entity.ContentType,
            FileSizeBytes = entity.FileSizeBytes,
            BlobUri = entity.BlobUri,
            UploadedAt = entity.UploadedAt,
            UploadedBy = entity.UploadedBy
        };
}
