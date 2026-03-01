namespace Application.Models;

public record TagDto : EntityBaseDto
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}
