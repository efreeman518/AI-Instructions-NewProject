// Pattern: Enum for notification/scheduling event types.

namespace Domain.Model.Enums;

public enum ReminderType
{
    /// <summary>Reminder fires at a specific date/time.</summary>
    OneTime = 0,

    /// <summary>Reminder fires on a recurring schedule (e.g., daily standup reminder).</summary>
    Recurring = 1
}
