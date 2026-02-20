// Pattern: [Flags] enum with bit-shift values for combinable state.
// Uses 1 << N for clarity and correctness. Never use raw integers.
// The entity exposes convenience bool properties (IsCompleted, IsBlocked, etc.)
// for readable code throughout the codebase.

namespace Domain.Model.Enums;

[Flags]
public enum TodoItemStatus
{
    /// <summary>Default state — newly created, not yet started.</summary>
    None = 0,

    /// <summary>Work has begun on this item.</summary>
    IsStarted = 1 << 0,    // 1

    /// <summary>Item has been completed successfully.</summary>
    IsCompleted = 1 << 1,  // 2

    /// <summary>Item is blocked by a dependency or external factor.</summary>
    IsBlocked = 1 << 2,    // 4

    /// <summary>Item has been soft-archived (hidden from active views).</summary>
    IsArchived = 1 << 3,   // 8

    /// <summary>Item has been cancelled — terminal state.</summary>
    IsCancelled = 1 << 4   // 16
}
