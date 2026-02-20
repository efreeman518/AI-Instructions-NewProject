// Pattern: Time-based entity DTO — Reminder.

using Domain.Model.Enums;

namespace Application.Models.Reminder;

public class ReminderDto
{
    public Guid Id { get; set; }
    public Guid TodoItemId { get; set; }
    public ReminderType ReminderType { get; set; }
    public DateTimeOffset ReminderDateUtc { get; set; }

    /// <summary>Pattern: Cron expression for recurring schedules (e.g., "0 9 * * MON").</summary>
    public string? CronExpression { get; set; }

    public string? Message { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastFiredUtc { get; set; }
}
