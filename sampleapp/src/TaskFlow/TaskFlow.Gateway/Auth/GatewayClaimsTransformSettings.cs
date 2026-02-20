// ═══════════════════════════════════════════════════════════════
// Pattern: Gateway claims transform settings — configuration model.
// Specifies the header name and Gateway's Entra ID app registration.
// Binds from "GatewayTransformClaimsSettings" config section.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.Gateway.Auth;

/// <summary>
/// Pattern: Settings class for Gateway claim forwarding configuration.
/// Used by YARP transforms to know which header to populate.
/// </summary>
public class GatewayClaimsTransformSettings
{
    public const string ConfigSectionName = "GatewayTransformClaimsSettings";

    /// <summary>HTTP header name for serialized user claims (default: X-Orig-Request).</summary>
    public string HeaderName { get; set; } = "X-Orig-Request";

    /// <summary>Entra ID application (client) ID of this Gateway registration.</summary>
    public string GatewayAppId { get; set; } = string.Empty;
}
