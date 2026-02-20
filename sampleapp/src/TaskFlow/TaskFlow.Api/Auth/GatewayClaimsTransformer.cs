// ═══════════════════════════════════════════════════════════════
// Pattern: Claims transformer — API-side hydration of forwarded user claims.
// The Gateway authenticates the external user and forwards their claims
// as JSON in the X-Orig-Request header. This IClaimsTransformation
// deserializes those claims and merges them into the service-to-service
// ClaimsPrincipal so IRequestContext sees the real user identity.
// ═══════════════════════════════════════════════════════════════

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TaskFlow.Api.Auth;

/// <summary>
/// Pattern: IClaimsTransformation — runs after JWT validation on every request.
/// 1. Verify the caller IS the Gateway (by matching "azp"/"appid" claim to config).
/// 2. Read the <see cref="GatewayClaimsTransformSettings.HeaderName"/> header.
/// 3. Deserialize <see cref="GatewayClaimsPayload"/>.
/// 4. Merge user-level claims (roles, tenantId, email, etc.) into the principal.
/// </summary>
public class GatewayClaimsTransformer(
    ILogger<GatewayClaimsTransformer> logger,
    IHttpContextAccessor httpContextAccessor,
    IOptions<GatewayClaimsTransformSettings> settings) : IClaimsTransformation
{
    private readonly GatewayClaimsTransformSettings _settings = settings.Value;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return Task.FromResult(principal);

        // Pattern: Only transform claims when the caller is the Gateway.
        if (!IsGatewayCaller(principal))
            return Task.FromResult(principal);

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null ||
            !httpContext.Request.Headers.TryGetValue(_settings.HeaderName, out var headerValues))
            return Task.FromResult(principal);

        var headerJson = headerValues.FirstOrDefault();
        if (string.IsNullOrEmpty(headerJson))
            return Task.FromResult(principal);

        // Pattern: AOT-safe deserialization via source-generated JsonSerializerContext.
        GatewayClaimsPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize(headerJson, GatewayClaimsJsonContext.Default.GatewayClaimsPayload);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize {HeaderName} header JSON", _settings.HeaderName);
        }

        if (payload is null)
            return Task.FromResult(principal);

        // Pattern: Clone identity and merge forwarded claims (avoid duplicates).
        var newIdentity = identity.Clone();

        if (payload.UserRoles is { Length: > 0 })
        {
            foreach (var role in payload.UserRoles)
                AddIfMissing(newIdentity, ClaimTypes.Role, role);
        }

        AddIfMissing(newIdentity, "userTenantId", payload.UserTenantId);
        AddIfMissing(newIdentity, "sub", payload.Sub);
        AddIfMissing(newIdentity, ClaimTypes.Email, payload.Email);
        AddIfMissing(newIdentity, ClaimTypes.Name, payload.Name);
        AddIfMissing(newIdentity, "oid", payload.Oid);

        return Task.FromResult(new ClaimsPrincipal(newIdentity));
    }

    /// <summary>
    /// Pattern: Verify the authenticated caller is the Gateway service principal.
    /// Checks "azp" (OAuth2) or "appid" (legacy) claim against configured GatewayAppId.
    /// </summary>
    private bool IsGatewayCaller(ClaimsPrincipal user) =>
        user.HasClaim(c => (c.Type == "azp" || c.Type == "appid") && c.Value == _settings.GatewayAppId);

    /// <summary>
    /// Pattern: Add claim only if not already present — prevents duplicate claims.
    /// </summary>
    private static void AddIfMissing(ClaimsIdentity identity, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !identity.HasClaim(c => c.Type == type && c.Value == value))
            identity.AddClaim(new Claim(type, value!));
    }
}
