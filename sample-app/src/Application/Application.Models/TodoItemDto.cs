namespace Application.Models;

public record TodoItemDto : EntityBaseDto, ITenantEntityDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(DomainConstants.TITLE_MAX_LENGTH)]
    public string Title { get; set; } = null!;

    [MaxLength(DomainConstants.DESCRIPTION_MAX_LENGTH)]
    public string? Description { get; set; }

    public int Priority { get; set; } = DomainConstants.PRIORITY_DEFAULT;
    public TodoItemStatus Status { get; set; } = TodoItemStatus.None;
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }

    // Foreign keys
    public Guid? ParentId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? TeamId { get; set; }

    // Nested DTOs
    public CategoryDto? Category { get; set; }
    public TeamMemberDto? AssignedTo { get; set; }
    public List<CommentDto> Comments { get; set; } = [];
    public List<ReminderDto> Reminders { get; set; } = [];
    public List<TagDto> Tags { get; set; } = [];
    public List<AttachmentDto> Attachments { get; set; } = [];
}
