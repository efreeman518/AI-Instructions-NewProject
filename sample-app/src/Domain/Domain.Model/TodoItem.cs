namespace Domain.Model;

public class TodoItem : EntityBase, ITenantEntity<Guid>
{
    public static DomainResult<TodoItem> Create(Guid tenantId, string title, string? description = null,
        int priority = DomainConstants.PRIORITY_DEFAULT, Guid? categoryId = null, Guid? teamId = null)
    {
        var entity = new TodoItem(tenantId, title, description, priority, categoryId, teamId);
        return entity.Valid().Map(_ => entity);
    }

    private TodoItem(Guid tenantId, string title, string? description, int priority, Guid? categoryId, Guid? teamId)
    {
        TenantId = tenantId;
        Title = title;
        Description = description;
        Priority = priority;
        CategoryId = categoryId;
        TeamId = teamId;
    }

    // EF-compatible constructor
    private TodoItem() { }

    public Guid TenantId { get; init; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public int Priority { get; private set; } = DomainConstants.PRIORITY_DEFAULT;
    public TodoItemStatus Status { get; private set; } = TodoItemStatus.None;
    public decimal? EstimatedHours { get; private set; }
    public decimal? ActualHours { get; private set; }
    public DateRange? Schedule { get; private set; }

    // Foreign keys
    public Guid? ParentId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? AssignedToId { get; private set; }
    public Guid? TeamId { get; private set; }

    // Navigation properties
    public TodoItem? Parent { get; private set; }
    public ICollection<TodoItem> Children { get; private set; } = [];
    public Category? Category { get; private set; }
    public TeamMember? AssignedTo { get; private set; }
    public Team? Team { get; private set; }
    public ICollection<Comment> Comments { get; private set; } = [];
    public ICollection<Reminder> Reminders { get; private set; } = [];
    public ICollection<Tag> Tags { get; private set; } = [];
    public ICollection<Attachment> Attachments { get; private set; } = [];
    public ICollection<TodoItemHistory> History { get; private set; } = [];

    public DomainResult<TodoItem> Update(string? title = null, string? description = null, int? priority = null,
        decimal? estimatedHours = null, decimal? actualHours = null, Guid? categoryId = null, Guid? teamId = null)
    {
        if (title is not null) Title = title;
        if (description is not null) Description = description;
        if (priority.HasValue) Priority = priority.Value;
        if (estimatedHours.HasValue) EstimatedHours = estimatedHours.Value;
        if (actualHours.HasValue) ActualHours = actualHours.Value;
        if (categoryId.HasValue) CategoryId = categoryId.Value;
        if (teamId.HasValue) TeamId = teamId.Value;
        return Valid();
    }

    public DomainResult<TodoItem> SetSchedule(DateTimeOffset? startDate = null, DateTimeOffset? dueDate = null)
    {
        Schedule = Schedule?.Update(startDate, dueDate) ?? DateRange.Create(startDate, dueDate);
        return DomainResult<TodoItem>.Success(this);
    }

    // ── State machine transitions ────────────────────────────

    public DomainResult<TodoItem> Start()
    {
        if (Status != TodoItemStatus.None)
            return DomainResult<TodoItem>.Failure("Can only start an item with status None.");
        Status = TodoItemStatus.IsStarted;
        return DomainResult<TodoItem>.Success(this);
    }

    public DomainResult<TodoItem> Complete()
    {
        if (Status != TodoItemStatus.IsStarted)
            return DomainResult<TodoItem>.Failure("Can only complete a started item.");
        Status = TodoItemStatus.IsCompleted;
        return DomainResult<TodoItem>.Success(this);
    }

    public DomainResult<TodoItem> Block()
    {
        if (Status != TodoItemStatus.None && Status != TodoItemStatus.IsStarted)
            return DomainResult<TodoItem>.Failure("Can only block an item with status None or Started.");
        Status = TodoItemStatus.IsBlocked;
        return DomainResult<TodoItem>.Success(this);
    }

    public DomainResult<TodoItem> Unblock()
    {
        if (Status != TodoItemStatus.IsBlocked)
            return DomainResult<TodoItem>.Failure("Can only unblock a blocked item.");
        Status = TodoItemStatus.IsStarted;
        return DomainResult<TodoItem>.Success(this);
    }

    public DomainResult<TodoItem> Cancel()
    {
        if (Status == TodoItemStatus.IsCompleted || Status == TodoItemStatus.IsArchived)
            return DomainResult<TodoItem>.Failure("Cannot cancel a completed or archived item.");
        Status = TodoItemStatus.IsCancelled;
        return DomainResult<TodoItem>.Success(this);
    }

    public DomainResult<TodoItem> Archive()
    {
        if (Status != TodoItemStatus.None && Status != TodoItemStatus.IsCompleted)
            return DomainResult<TodoItem>.Failure("Can only archive items with status None or Completed.");
        Status = TodoItemStatus.IsArchived;
        return DomainResult<TodoItem>.Success(this);
    }

    public DomainResult<TodoItem> Restore()
    {
        if (Status != TodoItemStatus.IsArchived)
            return DomainResult<TodoItem>.Failure("Can only restore archived items.");
        Status = TodoItemStatus.None;
        return DomainResult<TodoItem>.Success(this);
    }

    public DomainResult<TodoItem> Reopen()
    {
        if (Status == TodoItemStatus.IsCompleted)
        {
            Status = TodoItemStatus.IsStarted;
            return DomainResult<TodoItem>.Success(this);
        }
        if (Status == TodoItemStatus.IsCancelled)
        {
            Status = TodoItemStatus.None;
            return DomainResult<TodoItem>.Success(this);
        }
        return DomainResult<TodoItem>.Failure("Can only reopen completed or cancelled items.");
    }

    public DomainResult<TodoItem> Reset()
    {
        if (Status != TodoItemStatus.IsStarted && Status != TodoItemStatus.IsBlocked)
            return DomainResult<TodoItem>.Failure("Can only reset Started or Blocked items.");
        Status = TodoItemStatus.None;
        return DomainResult<TodoItem>.Success(this);
    }

    // ── Assignment ───────────────────────────────────────────

    public DomainResult<TodoItem> Assign(Guid? assignedToId)
    {
        AssignedToId = assignedToId;
        return DomainResult<TodoItem>.Success(this);
    }

    // ── Hierarchy ────────────────────────────────────────────

    public DomainResult<TodoItem> SetParent(Guid? parentId)
    {
        if (parentId.HasValue && parentId.Value == Id)
            return DomainResult<TodoItem>.Failure("An item cannot be its own parent.");
        ParentId = parentId;
        return DomainResult<TodoItem>.Success(this);
    }

    // ── Child collection management ──────────────────────────

    public DomainResult<Comment> AddComment(string text, Guid authorId)
    {
        var result = Comment.Create(TenantId, Id, text, authorId);
        if (result.IsFailure) return result;
        Comments.Add(result.Value!);
        return result;
    }

    public DomainResult<Reminder> AddReminder(ReminderType type, DateTimeOffset? remindAt = null, string? cronExpression = null, string? message = null)
    {
        var result = Reminder.Create(TenantId, Id, type, remindAt, cronExpression, message);
        if (result.IsFailure) return result;
        Reminders.Add(result.Value!);
        return result;
    }

    public DomainResult RemoveReminder(Guid reminderId)
    {
        var toRemove = Reminders.FirstOrDefault(r => r.Id == reminderId);
        if (toRemove != null) Reminders.Remove(toRemove);
        return DomainResult.Success();
    }

    public void AddTag(Tag tag) => Tags.Add(tag);
    public void RemoveTag(Tag tag) => Tags.Remove(tag);

    // ── Flags helpers ────────────────────────────────────────

    public void AddStatusFlag(TodoItemStatus flag) => Status |= flag;
    public void RemoveStatusFlag(TodoItemStatus flag) => Status &= ~flag;

    // ── Validation ───────────────────────────────────────────

    private DomainResult<TodoItem> Valid()
    {
        var errors = new List<DomainError>();
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > DomainConstants.TITLE_MAX_LENGTH)
            errors.Add(DomainError.Create("Title is required and must be 200 characters or fewer."));
        if (Description is not null && Description.Length > DomainConstants.DESCRIPTION_MAX_LENGTH)
            errors.Add(DomainError.Create("Description must be 2000 characters or fewer."));
        if (Priority < DomainConstants.PRIORITY_MIN || Priority > DomainConstants.PRIORITY_MAX)
            errors.Add(DomainError.Create("Priority must be between 1 and 5."));
        if (EstimatedHours.HasValue && EstimatedHours.Value < 0)
            errors.Add(DomainError.Create("Estimated hours cannot be negative."));
        if (ActualHours.HasValue && ActualHours.Value < 0)
            errors.Add(DomainError.Create("Actual hours cannot be negative."));
        if (Status.HasFlag(TodoItemStatus.IsCompleted) && Status.HasFlag(TodoItemStatus.IsBlocked))
            errors.Add(DomainError.Create("An item cannot be completed and blocked at the same time."));
        if (Status.HasFlag(TodoItemStatus.IsCancelled) && Status.HasFlag(TodoItemStatus.IsStarted))
            errors.Add(DomainError.Create("A cancelled item cannot also be started."));
        if (ParentId.HasValue && ParentId.Value == Id)
            errors.Add(DomainError.Create("An item cannot be its own parent."));
        return (errors.Count > 0)
            ? DomainResult<TodoItem>.Failure(errors)
            : DomainResult<TodoItem>.Success(this);
    }
}
