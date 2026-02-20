// ═══════════════════════════════════════════════════════════════
// Pattern: Gateway claims transform settings — configuration model.
// Binds from "GatewayTransformClaimsSettings" config section.
// Specifies which header carries the forwarded claims and the Gateway's app ID.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.Api.Auth;

/// <summary>
/// Pattern: Settings class with <see cref="ConfigSectionName"/> constant.
/// Bound via <c>services.Configure&lt;GatewayClaimsTransformSettings&gt;(config.GetSection(...))</c>.
/// <list type="bullet">
///   <item><see cref="HeaderName"/>: HTTP header containing serialized user claims (default: X-Orig-Request).</item>
///   <item><see cref="GatewayAppId"/>: Entra ID application (client) ID of the Gateway — used to verify the caller is the Gateway.</item>
/// </list>
/// </summary>
public class GatewayClaimsTransformSettings
{
    public const string ConfigSectionName = "GatewayTransformClaimsSettings";

    /// <summary>HTTP header name carrying the serialized <see cref="GatewayClaimsPayload"/> JSON.</summary>
    public string HeaderName { get; set; } = "X-Orig-Request";

    /// <summary>Entra ID application (client) ID of the Gateway service principal.</summary>
    public string GatewayAppId { get; set; } = string.Empty;
}
