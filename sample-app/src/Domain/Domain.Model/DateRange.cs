namespace Domain.Model;

/// <summary>
/// Value object representing a date range with start and due dates.
/// </summary>
public sealed class DateRange
{
    public DateTimeOffset? StartDate { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }

    private DateRange() { }

    public static DateRange Create(DateTimeOffset? startDate = null, DateTimeOffset? dueDate = null)
    {
        if (startDate.HasValue && dueDate.HasValue && startDate > dueDate)
            throw new ArgumentException("StartDate cannot be after DueDate.");

        return new DateRange { StartDate = startDate, DueDate = dueDate };
    }

    public DateRange Update(DateTimeOffset? startDate = null, DateTimeOffset? dueDate = null)
    {
        var newStart = startDate ?? StartDate;
        var newDue = dueDate ?? DueDate;
        return Create(newStart, newDue);
    }

    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTimeOffset.UtcNow;
    public bool HasStarted => StartDate.HasValue && StartDate.Value <= DateTimeOffset.UtcNow;
}
