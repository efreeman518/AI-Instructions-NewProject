// ═══════════════════════════════════════════════════════════════
// Pattern: Authorization Handler — Tenant match.
// Verifies the route tenantId matches the JWT "userTenantId" claim.
// Prevents cross-tenant data access at the API boundary.
// ═══════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Authorization;

namespace TaskFlow.Api.Auth;

/// <summary>
/// Pattern: Custom IAuthorizationRequirement + IAuthorizationHandler pair.
/// TenantMatchRequirement is the marker; TenantMatchHandler checks the claim.
/// Registered in RegisterApiServices.AddAuthentication via policy builder.
/// </summary>
public class TenantMatchRequirement : IAuthorizationRequirement;

/// <summary>
/// Compares the route {tenantId} against the JWT "userTenantId" claim.
/// Succeeds if: they match OR user has GlobalAdmin role (bypass).
/// Fails if: claim is missing or values mismatch.
/// </summary>
public class TenantMatchHandler(ILogger<TenantMatchHandler> logger) : AuthorizationHandler<TenantMatchRequirement>
{
    private const string TenantClaimType = "userTenantId";
    private const string GlobalAdminRole = "GlobalAdmin";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TenantMatchRequirement requirement)
    {
        // Pattern: GlobalAdmin bypasses all tenant checks.
        if (context.User.IsInRole(GlobalAdminRole))
        {
            logger.LogDebug("GlobalAdmin bypass — tenant match skipped");
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Pattern: Extract route tenantId from HttpContext.
        if (context.Resource is not HttpContext httpContext)
        {
            logger.LogWarning("TenantMatch: Unable to retrieve HttpContext from auth resource");
            context.Fail(new AuthorizationFailureReason(this, "Unable to resolve HttpContext"));
            return Task.CompletedTask;
        }

        var routeTenantId = httpContext.GetRouteValue("tenantId")?.ToString();
        if (string.IsNullOrWhiteSpace(routeTenantId))
        {
            // Pattern: No tenantId in route — endpoint is non-tenant (e.g., Tags).
            // Non-tenant routes don't use the TenantMatch policy, but handle gracefully.
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var claimTenantId = context.User.FindFirst(TenantClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(claimTenantId))
        {
            logger.LogWarning("TenantMatch: Missing {ClaimType} claim for user {User}",
                TenantClaimType, context.User.Identity?.Name);
            context.Fail(new AuthorizationFailureReason(this, $"Missing {TenantClaimType} claim"));
            return Task.CompletedTask;
        }

        if (string.Equals(routeTenantId, claimTenantId, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning("TenantMatch: Route tenantId {RouteTenant} does not match claim {ClaimTenant}",
                routeTenantId, claimTenantId);
            context.Fail(new AuthorizationFailureReason(this,
                $"Tenant mismatch: route={routeTenantId}, claim={claimTenantId}"));
        }

        return Task.CompletedTask;
    }
}
