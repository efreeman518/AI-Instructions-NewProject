// ═══════════════════════════════════════════════════════════════
// Pattern: Tenant match authorization — Gateway side.
// Same logic as the API's TenantMatchHandler but for user-facing tokens.
// Checks route {tenantId} against JWT "userTenantId" claim.
// ═══════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Authorization;

namespace TaskFlow.Gateway.Auth;

/// <summary>
/// Pattern: IAuthorizationRequirement marker — used by TenantMatchPolicy.
/// </summary>
public class TenantMatchRequirement : IAuthorizationRequirement;

/// <summary>
/// Pattern: Tenant boundary enforcement at the Gateway level.
/// Compares the route {tenantId} against the JWT "userTenantId" claim.
/// GlobalAdmin role bypasses the check.
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
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            context.Fail(new AuthorizationFailureReason(this, "Unable to resolve HttpContext"));
            return Task.CompletedTask;
        }

        var routeTenantId = httpContext.GetRouteValue("tenantId")?.ToString();
        if (string.IsNullOrWhiteSpace(routeTenantId))
        {
            // Pattern: No tenantId in route — non-tenant endpoint, succeed.
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var claimTenantId = context.User.FindFirst(TenantClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(claimTenantId))
        {
            logger.LogWarning("TenantMatch: Missing {ClaimType} claim", TenantClaimType);
            context.Fail(new AuthorizationFailureReason(this, $"Missing {TenantClaimType} claim"));
            return Task.CompletedTask;
        }

        if (string.Equals(routeTenantId, claimTenantId, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning("TenantMatch: Route {RouteTenant} ≠ claim {ClaimTenant}",
                routeTenantId, claimTenantId);
            context.Fail(new AuthorizationFailureReason(this,
                $"Tenant mismatch: route={routeTenantId}, claim={claimTenantId}"));
        }

        return Task.CompletedTask;
    }
}
