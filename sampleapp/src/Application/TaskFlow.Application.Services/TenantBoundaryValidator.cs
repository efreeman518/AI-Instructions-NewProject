// ═══════════════════════════════════════════════════════════════
// Pattern: Tenant boundary validator implementation — delegates to static ValidationHelper.
// Registered as Scoped in Bootstrapper.
// Services inject ITenantBoundaryValidator instead of calling ValidationHelper directly,
// enabling testability (mock the interface in unit tests).
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Services;
using Application.Services.Rules;
using Microsoft.Extensions.Logging;
using EF.Common.Contracts;

namespace Application.Services;

/// <summary>
/// Pattern: Thin wrapper — delegates all logic to static <see cref="ValidationHelper"/>.
/// The wrapper exists solely for DI/testability — the static helper holds the actual logic.
/// </summary>
public sealed class TenantBoundaryValidator : ITenantBoundaryValidator
{
    public Result EnsureTenantBoundary(
        ILogger logger, Guid? callerTenantId, IReadOnlyCollection<string> callerRoles,
        Guid? entityTenantId, string operation, string entityName, Guid? entityId = null)
        => ValidationHelper.EnsureTenantBoundary(logger, callerTenantId, callerRoles,
            entityTenantId, operation, entityName, entityId);

    public Result EnsureGlobalAdmin(IReadOnlyCollection<string> callerRoles, string operation)
        => ValidationHelper.EnsureGlobalAdmin(callerRoles, operation);

    public Result PreventTenantChange(
        ILogger logger, Guid? existingTenantId, Guid? incomingTenantId,
        string entityName, Guid entityId)
        => ValidationHelper.PreventTenantChange(logger, existingTenantId, incomingTenantId, entityName, entityId);
}
