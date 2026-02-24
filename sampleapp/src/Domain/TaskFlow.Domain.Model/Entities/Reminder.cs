// Pattern: Time-based entity — demonstrates scheduler/notification integration.
// Reminders are scheduled via TickerQ (ITimeTickerManager for one-off,
// ICronTickerManager for recurring). When the reminder fires, it triggers
// a notification to the assigned user.

using Domain.Model.Enums;
using EF.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// Reminder entity — schedules a notification for a TodoItem at a specific time.
/// Triggers background job execution via TickerQ scheduler integration.
/// </summary>
public class Reminder : EntityBase, ITenantEntity<Guid>
{
    public Guid TenantId { get; init; }

    /// <summary>FK to the TodoItem this reminder is for.</summary>
    public Guid TodoItemId { get; init; }

    /// <summary>One-time or recurring reminder.</summary>
    public ReminderType Type { get; private set; }

    /// <summary>When the reminder should fire (for one-time reminders).</summary>
    public DateTimeOffset? RemindAt { get; private set; }

    /// <summary>Cron expression (for recurring reminders, e.g., "0 9 * * MON" = 9am Mondays).</summary>
    public string? CronExpression { get; private set; }

    /// <summary>Custom message for the notification. Optional — defaults to item title.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether this reminder has been sent (for one-time) or is still active (recurring).</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>When the last notification was sent for this reminder.</summary>
    public DateTimeOffset? LastFiredAt { get; private set; }

    // Navigation
    public TodoItem TodoItem { get; private set; } = null!;

    private Reminder() { }

    public static DomainResult<Reminder> Create(
        Guid tenantId,
        Guid todoItemId,
        ReminderType type,
        DateTimeOffset? remindAt,
        string? cronExpression,
        string? message)
    {
        var entity = new Reminder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TodoItemId = todoItemId,
            Type = type,
            RemindAt = remindAt,
            CronExpression = cronExpression,
            Message = message,
            IsActive = true
        };

        return entity.Valid() ? DomainResult<Reminder>.Success(entity)
                              : DomainResult<Reminder>.Failure(entity._validationErrors);
    }

    /// <summary>
    /// Marks the reminder as fired. For one-time reminders, this deactivates it.
    /// For recurring, it just updates LastFiredAt.
    /// </summary>
    public void MarkFired()
    {
        LastFiredAt = DateTimeOffset.UtcNow;
        if (Type == ReminderType.OneTime)
        {
            IsActive = false;
        }
    }

    public void Deactivate() => IsActive = false;

    private List<string> _validationErrors = [];

    private bool Valid()
    {
        _validationErrors = [];

        if (Type == ReminderType.OneTime && !RemindAt.HasValue)
            _validationErrors.Add("One-time reminders require a RemindAt date.");

        if (Type == ReminderType.OneTime && RemindAt.HasValue && RemindAt.Value <= DateTimeOffset.UtcNow)
            _validationErrors.Add("RemindAt must be in the future.");

        if (Type == ReminderType.Recurring && string.IsNullOrWhiteSpace(CronExpression))
            _validationErrors.Add("Recurring reminders require a CronExpression.");

        if (Message?.Length > 500)
            _validationErrors.Add("Message must not exceed 500 characters.");

        return _validationErrors.Count == 0;
    }
}
