namespace Application.Models;

public record CommentDto : EntityBaseDto, ITenantEntityDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid TodoItemId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Text { get; set; } = null!;

    [Required]
    public Guid AuthorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
