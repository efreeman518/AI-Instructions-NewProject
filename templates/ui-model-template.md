# UI Business Model Template

| | |
|---|---|
| **File** | `Business/Models/{Entity}.cs` |
| **Depends on** | [dto-template](dto-template.md) (API DTO structure) |
| **Referenced by** | [ui-service-template](ui-service-template.md), [mvux-model-template](mvux-model-template.md) |

## Client-Side Record Model

```csharp
using {Project}.UI.Client.Models;
using {Entity}Data = {Project}.UI.Client.Models.{Entity}Data;

namespace {Project}.UI.Business.Models;

/// <summary>
/// Client-side immutable record for {Entity}.
/// Wraps the Kiota-generated wire DTO ({Entity}Data).
/// </summary>
public partial record {Entity} : IEntityBase
{
    /// <summary>
    /// Create from Kiota wire DTO.
    /// </summary>
    internal {Entity}({Entity}Data data)
    {
        Id = data.Id ?? Guid.Empty;
        Name = data.Name;
        // Map all properties from data → record
    }

    // Default constructor for create scenarios
    public {Entity}() { }

    public Guid Id { get; init; }
    public string? Name { get; init; }
    public bool IsFavorite { get; init; }
    // ... add all entity properties

    // Computed properties (display helpers)
    // public string DisplayText => $"{Name} — {SomeOtherProp}";

    /// <summary>
    /// Convert back to Kiota wire DTO for POST/PUT requests.
    /// </summary>
    internal {Entity}Data ToData() => new()
    {
        Id = Id,
        Name = Name,
        // Map all properties from record → data
    };
}
```

## Shared Interface

```csharp
namespace {Project}.UI.Business.Models;

/// <summary>
/// Marker interface for entities with a Guid Id.
/// Used by EntityMessage<T> and messenger-based refresh.
/// </summary>
public interface IEntityBase
{
    Guid Id { get; }
}
```

## Entity Message

```csharp
namespace {Project}.UI.Presentation.Messages;

public enum EntityChange { Created, Updated, Deleted }

/// <summary>
/// Broadcast via IMessenger when an entity is mutated.
/// MVUX models using .Observe() auto-refresh on receipt.
/// </summary>
public record EntityMessage<T>(EntityChange Change, T Entity);
```

## Rules

- Use `partial record` with `init` properties — immutable by default
- Provide an `internal` constructor that accepts the Kiota wire DTO (`{Entity}Data`)
- Provide a `ToData()` method to convert back to the wire DTO
- Keep computed/display properties as expression-bodied getters
- Implement `IEntityBase` so messaging key selectors work
- Default constructor (parameterless) is needed for create/form scenarios
- Use `using {Entity}Data = ...` alias to avoid naming collisions with the client record
