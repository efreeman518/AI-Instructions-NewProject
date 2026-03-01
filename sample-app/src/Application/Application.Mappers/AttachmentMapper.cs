namespace Application.Mappers;

public static class AttachmentMapper
{
    public static AttachmentDto ToDto(this Attachment entity) =>
        new()
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
            UploadedBy = entity.UploadedBy,
        };
}
