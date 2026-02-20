// ═══════════════════════════════════════════════════════════════
// Pattern: Gateway claims payload — Gateway-side model.
// Serialized into X-Orig-Request header by YARP request transform.
// Matches the API's GatewayClaimsPayload for contract alignment.
// ═══════════════════════════════════════════════════════════════

using System.Text.Json.Serialization;

namespace TaskFlow.Gateway.Auth;

/// <summary>
/// Pattern: Claims payload model — represents the user identity claims
/// extracted from the external JWT and forwarded to downstream APIs.
/// Serialized by the YARP transform in RegisterServices.ConfigureProxyTransforms.
/// </summary>
public sealed class GatewayClaimsPayload
{
    public string? Sub { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Oid { get; set; }
    public string? UserTenantId { get; set; }
    public string[]? UserRoles { get; set; }
}

/// <summary>
/// Pattern: Source-generated JSON context for AOT-safe serialization.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GatewayClaimsPayload))]
internal partial class GatewayClaimsJsonContext : JsonSerializerContext;
