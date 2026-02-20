// ═══════════════════════════════════════════════════════════════
// Pattern: Structured validation for create/update DTOs.
// Returns Result (not exceptions) — composable with Result.Combine.
// ═══════════════════════════════════════════════════════════════

using Package.Infrastructure.Common.Contracts;
using Package.Infrastructure.Domain.Contracts;

namespace Application.Services.Rules;

/// <summary>
/// Pattern: Static structural validators — generic pre-condition checks
/// for DTO shape before domain entity creation/update.
/// Validates that required fields (Id, TenantId) are present.
/// </summary>
public static class StructureValidators
{
    /// <summary>Pattern: Guard — returns Failure if condition is false.</summary>
    internal static Result Require(bool condition, string errorMessage) =>
        condition ? Result.Success() : Result.Failure(errorMessage);

    /// <summary>
    /// Pattern: Validate create payload — DTO must be non-null with valid TenantId.
    /// TenantId is required because all entities in this domain are tenant-scoped.
    /// </summary>
    internal static Result ValidateCreate<T>(T? dto) where T : class, ITenantEntityDto
    {
        if (dto is null) return Result.Failure(ServiceErrorMessages.PayloadRequired(typeof(T).Name));
        return Require(dto.TenantId != Guid.Empty,
            ServiceErrorMessages.FieldRequired("TenantId"));
    }

    /// <summary>
    /// Pattern: Validate update payload — DTO must have valid Id AND TenantId.
    /// Id is required for lookup; TenantId for immutability check.
    /// </summary>
    internal static Result ValidateUpdate<T>(T? dto) where T : class, IEntityBaseDto, ITenantEntityDto
    {
        if (dto is null) return Result.Failure(ServiceErrorMessages.PayloadRequired(typeof(T).Name));
        return Result.Combine(
            Require(dto.Id.HasValue && dto.Id.Value != Guid.Empty,
                ServiceErrorMessages.FieldRequired("Id")),
            Require(dto.TenantId != Guid.Empty,
                ServiceErrorMessages.FieldRequired("TenantId"))
        );
    }
}
