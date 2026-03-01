namespace Application.Models;

public record AttachmentDto : EntityBaseDto, ITenantEntityDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid EntityId { get; set; }

    public AttachmentEntityType EntityType { get; set; }

    [Required]
    public string FileName { get; set; } = null!;

    [Required]
    public string ContentType { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    [Required]
    public string BlobUri { get; set; } = null!;

    public DateTimeOffset UploadedAt { get; set; }
    public Guid UploadedBy { get; set; }
}
