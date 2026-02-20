// ═══════════════════════════════════════════════════════════════
// Pattern: IEntityBase — marker interface for all client-side models.
// Used by EntityMessage<T> key selectors and messenger-based refresh.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI.Business.Models;

/// <summary>
/// Marker interface for entities with a Guid Id.
/// All client-side records implement this so MVUX .Observe() can key on Id.
/// </summary>
public interface IEntityBase
{
    Guid Id { get; }
}
