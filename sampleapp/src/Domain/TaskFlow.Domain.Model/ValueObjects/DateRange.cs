// Pattern: Value Object / Owned Type — represents a conceptual whole from its attributes.
// No identity of its own; stored as columns on the owning entity's table.
// Configured in EF via .OwnsOne() in the entity configuration.
// Immutable after creation — use record for structural equality.

namespace Domain.Model.ValueObjects;

/// <summary>
/// Represents a date range with optional start and required due date.
/// Owned by TodoItem — stored as columns StartDate/DueDate on the TodoItem table.
/// </summary>
public record DateRange
{
    /// <summary>When work should begin. Null means "no planned start".</summary>
    public DateTimeOffset? StartDate { get; init; }

    /// <summary>When the item is due. Null means "no deadline".</summary>
    public DateTimeOffset? DueDate { get; init; }

    // Pattern: Private parameterless constructor for EF Core materialization.
    private DateRange() { }

    /// <summary>
    /// Factory method with validation — mirrors the entity Create() pattern.
    /// </summary>
    public static DomainResult<DateRange> Create(DateTimeOffset? startDate, DateTimeOffset? dueDate)
    {
        // Pattern: Validate invariants before construction.
        if (startDate.HasValue && dueDate.HasValue && startDate.Value >= dueDate.Value)
        {
            return DomainResult<DateRange>.Failure("StartDate must be before DueDate.");
        }

        return DomainResult<DateRange>.Success(new DateRange
        {
            StartDate = startDate,
            DueDate = dueDate
        });
    }

    /// <summary>Convenience: is the due date in the past?</summary>
    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTimeOffset.UtcNow;
}
