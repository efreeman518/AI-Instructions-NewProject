// ═══════════════════════════════════════════════════════════════
// Pattern: EntityMessage — broadcast via IMessenger when an entity is mutated.
// MVUX models using .Observe(_messenger, item => item.Id) auto-refresh
// their Feed/State when a matching EntityMessage is received.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI.Presentation.Messages;

/// <summary>The type of entity mutation that occurred.</summary>
public enum EntityChange { Created, Updated, Deleted }

/// <summary>
/// Broadcast via IMessenger when an entity is created, updated, or deleted.
/// MVUX models using .Observe() auto-refresh on receipt.
/// </summary>
public record EntityMessage<T>(EntityChange Change, T Entity);
