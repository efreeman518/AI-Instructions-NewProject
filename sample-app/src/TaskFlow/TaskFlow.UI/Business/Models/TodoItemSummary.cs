using Domain.Shared;

namespace TaskFlow.UI.Business.Models;

/// <summary>
/// UI model for todo items — mapped from API DTOs at the service boundary.
/// </summary>
public partial record TodoItemSummary
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Priority { get; init; } = 3;
    public TodoItemStatus Status { get; init; }
    public decimal? EstimatedHours { get; init; }
    public decimal? ActualHours { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? AssignedToId { get; init; }
    public Guid? TeamId { get; init; }
    public string? CategoryName { get; init; }
    public string? AssignedToName { get; init; }
    public string? TeamName { get; init; }
    public int CommentCount { get; init; }
    public int TagCount { get; init; }

    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTimeOffset.UtcNow && !Status.HasFlag(TodoItemStatus.IsCompleted);
    public bool IsCompleted => Status.HasFlag(TodoItemStatus.IsCompleted);
    public bool IsBlocked => Status.HasFlag(TodoItemStatus.IsBlocked);

    public string StatusDisplay => Status switch
    {
        TodoItemStatus.None => "Not Started",
        _ when Status.HasFlag(TodoItemStatus.IsCompleted) => "Completed",
        _ when Status.HasFlag(TodoItemStatus.IsCancelled) => "Cancelled",
        _ when Status.HasFlag(TodoItemStatus.IsArchived) => "Archived",
        _ when Status.HasFlag(TodoItemStatus.IsBlocked) => "Blocked",
        _ when Status.HasFlag(TodoItemStatus.IsStarted) => "In Progress",
        _ => "Unknown"
    };

    public string PriorityDisplay => Priority switch
    {
        1 => "Critical",
        2 => "High",
        3 => "Medium",
        4 => "Low",
        5 => "Minimal",
        _ => "Medium"
    };
}
