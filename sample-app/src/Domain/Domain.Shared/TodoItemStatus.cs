namespace Domain.Shared;

[Flags]
public enum TodoItemStatus
{
    None = 0,
    IsStarted = 1 << 0,
    IsCompleted = 1 << 1,
    IsBlocked = 1 << 2,
    IsArchived = 1 << 3,
    IsCancelled = 1 << 4
}
