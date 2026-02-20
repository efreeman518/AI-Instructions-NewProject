// ═══════════════════════════════════════════════════════════════
// Pattern: Tenant boundary validator interface — in Application.Contracts.
// Injectable service that wraps tenant access validation logic.
// Allows services to delegate boundary checks without coupling to static helpers.
// ═══════════════════════════════════════════════════════════════

using Package.Infrastructure.Common.Contracts;

namespace Application.Contracts.Services;

/// <summary>
/// Pattern: Interface for tenant boundary validation — injected into services.
/// Encapsulates three core checks:
/// 1. EnsureTenantBoundary — caller can access the entity's tenant
/// 2. EnsureGlobalAdmin — operation requires GlobalAdmin role
/// 3. PreventTenantChange — TenantId is immutable after creation
/// Registered as Scoped in Bootstrapper.
/// </summary>
public interface ITenantBoundaryValidator
{
    /// <summary>
    /// Verifies that the caller's tenant matches the entity's tenant.
    /// GlobalAdmin bypasses. Null entityTenantId = global entity (admin-only).
    /// </summary>
    Result EnsureTenantBoundary(
        Microsoft.Extensions.Logging.ILogger logger,
        Guid? callerTenantId,
        IReadOnlyCollection<string> callerRoles,
        Guid? entityTenantId,
        string operation,
        string entityName,
        Guid? entityId = null);

    /// <summary>
    /// Verifies the caller has the GlobalAdmin role.
    /// Returns Failure with a descriptive message if not.
    /// </summary>
    Result EnsureGlobalAdmin(
        IReadOnlyCollection<string> callerRoles,
        string operation);

    /// <summary>
    /// Prevents changing TenantId on an existing entity.
    /// Returns Failure if existing ≠ incoming TenantId.
    /// </summary>
    Result PreventTenantChange(
        Microsoft.Extensions.Logging.ILogger logger,
        Guid? existingTenantId,
        Guid? incomingTenantId,
        string entityName,
        Guid entityId);
}
