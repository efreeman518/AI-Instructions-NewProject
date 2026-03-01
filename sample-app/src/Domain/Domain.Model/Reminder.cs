namespace Domain.Model;

public class Reminder : EntityBase, ITenantEntity<Guid>
{
    public static DomainResult<Reminder> Create(Guid tenantId, Guid todoItemId, ReminderType type,
        DateTimeOffset? remindAt = null, string? cronExpression = null, string? message = null)
    {
        var entity = new Reminder(tenantId, todoItemId, type, remindAt, cronExpression, message);
        return entity.Valid().Map(_ => entity);
    }

    private Reminder(Guid tenantId, Guid todoItemId, ReminderType type,
        DateTimeOffset? remindAt, string? cronExpression, string? message)
    {
        TenantId = tenantId;
        TodoItemId = todoItemId;
        Type = type;
        RemindAt = remindAt;
        CronExpression = cronExpression;
        Message = message;
    }

    // EF-compatible constructor
    private Reminder() { }

    public Guid TenantId { get; init; }
    public Guid TodoItemId { get; private set; }
    public ReminderType Type { get; private set; }
    public DateTimeOffset? RemindAt { get; private set; }
    public string? CronExpression { get; private set; }
    public string? Message { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastFiredAt { get; private set; }

    // Navigation
    public TodoItem TodoItem { get; private set; } = null!;

    public DomainResult MarkFired()
    {
        LastFiredAt = DateTimeOffset.UtcNow;
        if (Type == ReminderType.OneTime)
            IsActive = false;
        return DomainResult.Success();
    }

    public DomainResult<Reminder> Update(ReminderType? type = null, DateTimeOffset? remindAt = null,
        string? cronExpression = null, string? message = null, bool? isActive = null)
    {
        if (type.HasValue) Type = type.Value;
        if (remindAt.HasValue) RemindAt = remindAt.Value;
        if (cronExpression is not null) CronExpression = cronExpression;
        if (message is not null) Message = message;
        if (isActive.HasValue) IsActive = isActive.Value;
        return Valid();
    }

    private DomainResult<Reminder> Valid()
    {
        var errors = new List<DomainError>();
        if (Type == ReminderType.OneTime && !RemindAt.HasValue)
            errors.Add(DomainError.Create("One-time reminders require a RemindAt date."));
        if (Type == ReminderType.Recurring && string.IsNullOrWhiteSpace(CronExpression))
            errors.Add(DomainError.Create("Recurring reminders require a CronExpression."));
        return (errors.Count > 0)
            ? DomainResult<Reminder>.Failure(errors)
            : DomainResult<Reminder>.Success(this);
    }
}
