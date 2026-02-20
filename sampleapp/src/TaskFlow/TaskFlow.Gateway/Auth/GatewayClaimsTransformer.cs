// ═══════════════════════════════════════════════════════════════
// Pattern: Claims transformation — normalize B2C/Entra External claims.
// B2C uses non-standard claim types; this transformer maps them to standard
// types so authorization policies and IRequestContext work consistently.
// ═══════════════════════════════════════════════════════════════

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace TaskFlow.Gateway.Auth;

/// <summary>
/// Pattern: IClaimsTransformation — runs after JWT validation.
/// Maps B2C extension claims to standard claim types:
///   extension_Roles → ClaimTypes.Role
///   emails → ClaimTypes.Email
///   userTenantId → userTenantId (passthrough)
/// </summary>
public class GatewayClaimsTransformer(ILogger<GatewayClaimsTransformer> logger) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
            return Task.FromResult(principal);

        // Pattern: Map B2C extension_Roles claims to standard Role claims.
        // B2C custom attributes use "extension_{attributeName}" format.
        var roleClaims = identity.FindAll("extension_Roles").ToList();
        foreach (var roleClaim in roleClaims)
        {
            if (!identity.HasClaim(ClaimTypes.Role, roleClaim.Value))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));
                logger.LogDebug("Mapped extension_Roles → Role: {Role}", roleClaim.Value);
            }
        }

        // Pattern: Map B2C "emails" claim to standard Email claim.
        var emailClaim = identity.FindFirst("emails");
        if (emailClaim != null && !identity.HasClaim(ClaimTypes.Email, emailClaim.Value))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, emailClaim.Value));
        }

        return Task.FromResult(principal);
    }
}
