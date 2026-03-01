namespace Application.Models;

public record CategoryDto : EntityBaseDto, ITenantEntityDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
    public string? ColorHex { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
