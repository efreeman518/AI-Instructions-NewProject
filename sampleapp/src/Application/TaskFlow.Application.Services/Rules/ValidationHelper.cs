// ═══════════════════════════════════════════════════════════════
// Pattern: Validation helper — centralized service-layer validation.
// Static methods return Result — never throw exceptions.
// Used by all services for consistent pre-condition checks.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using EF.Common.Contracts;

namespace Application.Services.Rules;

/// <summary>
/// Pattern: Static validation helper — provides reusable validation functions
/// that return <see cref="Result"/> for consistent service-layer error handling.
/// Never throws exceptions — callers match on Result success/failure.
/// </summary>
public static class ValidationHelper
{
    // ═══════════════════════════════════════════════════════════════
    // Role-based access checks
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pattern: Role gate — verifies the caller has GlobalAdmin role.
    /// Used for operations that cross tenant boundaries (e.g., system-wide reports).
    /// </summary>
    public static Result EnsureGlobalAdmin(IReadOnlyCollection<string> callerRoles, string operation)
    {
        return callerRoles.Contains(Domain.Shared.Constants.Roles.GlobalAdmin)
            ? Result.Success()
            : Result.Failure($"Forbidden: Only a GlobalAdmin may perform this operation: {operation}.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Tenant boundary validation
    // Pattern: Prevents cross-tenant data access at the service layer.
    // GlobalAdmin bypasses; null entityTenantId = global entity (admin-only).
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pattern: Tenant boundary enforcement — compares caller's tenant with entity's tenant.
    /// Returns Failure if non-admin caller tries to access another tenant's entity.
    /// </summary>
    public static Result EnsureTenantBoundary(
        ILogger logger, Guid? callerTenantId, IReadOnlyCollection<string> callerRoles,
        Guid? entityTenantId, string operation, string entityName, Guid? entityId = null)
    {
        // Pattern: GlobalAdmin bypasses all tenant checks.
        if (callerRoles.Contains(Domain.Shared.Constants.Roles.GlobalAdmin))
            return Result.Success();

        if (callerRoles is null || callerRoles.Count == 0)
        {
            logger.LogWarning("Tenant boundary violation: Caller without roles. Op={Operation}, Entity={EntityName}, Id={EntityId}",
                operation, entityName, entityId);
            return Result.Failure($"Forbidden: Tenant boundary violation for operation: {operation}.");
        }

        // Pattern: null entityTenantId means a global entity — only GlobalAdmin can access.
        if (entityTenantId is null)
        {
            logger.LogWarning("Tenant boundary violation: Non-GlobalAdmin tried to access global entity. Op={Operation}, Entity={EntityName}, Id={EntityId}",
                operation, entityName, entityId);
            return Result.Failure($"Forbidden: Tenant boundary violation for operation: {operation}.");
        }

        if (callerTenantId.HasValue && callerTenantId.Value == entityTenantId)
            return Result.Success();

        logger.LogWarning("Tenant boundary violation: CallerTenant={CallerTenantId}, EntityTenant={EntityTenantId}, Op={Operation}, Entity={EntityName}, Id={EntityId}",
            callerTenantId, entityTenantId, operation, entityName, entityId);
        return Result.Failure($"Forbidden: Tenant boundary violation for operation: {operation}.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Tenant immutability
    // Pattern: Prevents changing TenantId on an existing entity —
    // TenantId is assigned at creation and never changes.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pattern: Immutable tenant assignment — TenantId cannot change after creation.
    /// Detects tenant spoofing by comparing existing vs incoming values.
    /// </summary>
    public static Result PreventTenantChange(
        ILogger logger, Guid? existingTenantId, Guid? incomingTenantId,
        string entityName, Guid entityId)
    {
        if (existingTenantId != incomingTenantId)
        {
            logger.LogTenantChangeAttempt(entityName, entityId, existingTenantId, incomingTenantId);
            return Result.Failure($"TenantId cannot be changed for an existing {entityName}.");
        }
        return Result.Success();
    }

    // ═══════════════════════════════════════════════════════════════
    // Payload validation helpers
    // Pattern: Null/empty/length checks — used before domain entity creation/update.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Pattern: Require non-null payload before processing.</summary>
    public static Result RequirePayload<T>(T? payload, string entityName) where T : class
    {
        return payload is not null
            ? Result.Success()
            : Result.Failure(ServiceErrorMessages.PayloadRequired(entityName));
    }

    /// <summary>Pattern: Require non-empty string field.</summary>
    public static Result RequireNonEmpty(string? value, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? Result.Success()
            : Result.Failure($"{fieldName} is required and cannot be empty.");
    }

    /// <summary>Pattern: Require max length — prevents oversized input.</summary>
    public static Result RequireMaxLength(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrEmpty(value)) return Result.Success(); // null/empty checked separately
        return value.Length <= maxLength
            ? Result.Success()
            : Result.Failure($"{fieldName} must not exceed {maxLength} characters.");
    }

    /// <summary>Pattern: Require valid GUID — rejects Guid.Empty and null.</summary>
    public static Result RequireValidId(Guid? id, string fieldName)
    {
        return id.HasValue && id.Value != Guid.Empty
            ? Result.Success()
            : Result.Failure($"{fieldName} is required and must be a valid identifier.");
    }

    /// <summary>
    /// Pattern: Combine multiple validation results — short-circuits on first failure if desired,
    /// or collects all errors. Uses Result.Combine from EF.
    /// </summary>
    public static Result ValidateAll(params Result[] results) => Result.Combine(results);
}
