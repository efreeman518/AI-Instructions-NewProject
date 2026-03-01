namespace Application.Models;

public record ReminderDto : EntityBaseDto, ITenantEntityDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid TodoItemId { get; set; }

    public ReminderType Type { get; set; }
    public DateTimeOffset? RemindAt { get; set; }
    public string? CronExpression { get; set; }
    public string? Message { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastFiredAt { get; set; }
}
