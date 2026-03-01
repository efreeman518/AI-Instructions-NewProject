namespace Domain.Model;

public class Attachment : EntityBase, ITenantEntity<Guid>
{
    public static DomainResult<Attachment> Create(Guid tenantId, Guid entityId, AttachmentEntityType entityType,
        string fileName, string contentType, long fileSizeBytes, string blobUri, Guid uploadedBy)
    {
        var entity = new Attachment(tenantId, entityId, entityType, fileName, contentType, fileSizeBytes, blobUri, uploadedBy);
        return entity.Valid().Map(_ => entity);
    }

    private Attachment(Guid tenantId, Guid entityId, AttachmentEntityType entityType,
        string fileName, string contentType, long fileSizeBytes, string blobUri, Guid uploadedBy)
    {
        TenantId = tenantId;
        EntityId = entityId;
        EntityType = entityType;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        BlobUri = blobUri;
        UploadedAt = DateTimeOffset.UtcNow;
        UploadedBy = uploadedBy;
    }

    // EF-compatible constructor
    private Attachment() { }

    public Guid TenantId { get; init; }
    public Guid EntityId { get; private set; }
    public AttachmentEntityType EntityType { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }
    public string BlobUri { get; private set; } = null!;
    public DateTimeOffset UploadedAt { get; private set; }
    public Guid UploadedBy { get; private set; }

    private DomainResult<Attachment> Valid()
    {
        var errors = new List<DomainError>();
        if (EntityId == Guid.Empty) errors.Add(DomainError.Create("EntityId cannot be empty."));
        if (string.IsNullOrWhiteSpace(FileName)) errors.Add(DomainError.Create("FileName is required."));
        if (string.IsNullOrWhiteSpace(ContentType)) errors.Add(DomainError.Create("ContentType is required."));
        if (FileSizeBytes <= 0) errors.Add(DomainError.Create("FileSizeBytes must be positive."));
        if (string.IsNullOrWhiteSpace(BlobUri)) errors.Add(DomainError.Create("BlobUri is required."));
        if (UploadedBy == Guid.Empty) errors.Add(DomainError.Create("UploadedBy is required."));
        return (errors.Count > 0)
            ? DomainResult<Attachment>.Failure(errors)
            : DomainResult<Attachment>.Success(this);
    }
}
