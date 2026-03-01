namespace Application.Models;

public record TeamDto : EntityBaseDto, ITenantEntityDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<TeamMemberDto> Members { get; set; } = [];
}
