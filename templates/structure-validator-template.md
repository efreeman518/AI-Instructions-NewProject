# Structure Validator Template

| | |
|---|---|
| **File** | `Application.Services/Rules/{Entity}StructureValidator.cs` |
| **Depends on** | [dto-template](dto-template.md) |
| **Referenced by** | [service-template](service-template.md), [application-layer.md](../skills/application-layer.md) |

## Purpose

Validates DTO structure (required fields, string lengths, enum ranges, child collection constraints) **before** domain factory/update calls. Static class — no DI registration needed.

Returns `Result<{Entity}Dto>` so services can short-circuit on invalid input without touching the domain layer.

## Template

```csharp
// File: Application.Services/Rules/{Entity}StructureValidator.cs
using Application.Models;

namespace Application.Services.Rules;

/// <summary>
/// Validates {Entity}Dto structure before domain operations.
/// </summary>
internal static class {Entity}StructureValidator
{
    public static Result<{Entity}Dto> ValidateCreate({Entity}Dto dto)
    {
        var errors = new List<string>();

        // Required fields
        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add("{Entity} name is required.");

        if (dto.TenantId == Guid.Empty)
            errors.Add("TenantId is required.");

        // String length limits (match EF MaxLength config)
        if (dto.Name?.Length > {NameMaxLength})
            errors.Add($"{Entity} name cannot exceed {NameMaxLength} characters.");

        if (dto.Description?.Length > {DescriptionMaxLength})
            errors.Add($"Description cannot exceed {DescriptionMaxLength} characters.");

        // Enum range
        // if (!Enum.IsDefined(typeof({Entity}Type), dto.Type))
        //     errors.Add($"Invalid {Entity} type: {dto.Type}.");

        // Child collection constraints
        // if (dto.{ChildEntity}s?.Count > {MaxChildren})
        //     errors.Add($"Cannot exceed {MaxChildren} {ChildEntity}s.");

        return errors.Count > 0
            ? Result<{Entity}Dto>.Failure(errors)
            : Result<{Entity}Dto>.Success(dto);
    }

    public static Result<{Entity}Dto> ValidateUpdate({Entity}Dto dto)
    {
        var errors = new List<string>();

        // Id required for updates
        if (dto.Id is null || dto.Id == Guid.Empty)
            errors.Add("{Entity} Id is required for updates.");

        // Reuse shared field checks
        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add("{Entity} name is required.");

        if (dto.Name?.Length > {NameMaxLength})
            errors.Add($"{Entity} name cannot exceed {NameMaxLength} characters.");

        if (dto.Description?.Length > {DescriptionMaxLength})
            errors.Add($"Description cannot exceed {DescriptionMaxLength} characters.");

        return errors.Count > 0
            ? Result<{Entity}Dto>.Failure(errors)
            : Result<{Entity}Dto>.Success(dto);
    }
}
```

## Rules

- **Static class** — no DI registration. Call directly: `{Entity}StructureValidator.ValidateCreate(dto)`.
- Keep validations purely structural (field presence, length, range). Domain invariants belong in [domain-rules-template](domain-rules-template.md).
- String `MaxLength` values must match the EF configuration in [ef-configuration-template](ef-configuration-template.md).
- Provide separate `ValidateCreate` and `ValidateUpdate` methods — update requires `Id`, create may have different required fields.
- Return all errors at once (don't short-circuit on first failure) so the caller gets a complete validation report.
- Uncomment enum/child constraints as needed per entity.
- **Contextual tokens:** `{NameMaxLength}`, `{DescriptionMaxLength}`, and `{MaxChildren}` are entity-specific — derive from the `maxLength` values in `resource-implementation.yaml`. These are NOT global placeholder tokens; replace with the literal integer for each entity (e.g., `200`, `2000`).
- Add or remove validated properties to match the entity's DTO — `dto.Description` is shown as an example; adjust to your entity's actual fields.

## Verification Checklist

- [ ] `ValidateCreate` checks all required fields for new entity creation
- [ ] `ValidateUpdate` requires `Id` and validates mutable fields
- [ ] String length limits match EF `MaxLength` configuration
- [ ] Returns `Result<{Entity}Dto>` consistent with service layer pattern
- [ ] Service template calls `ValidateCreate`/`ValidateUpdate` before domain operations
- [ ] No domain logic in validator — structural checks only
