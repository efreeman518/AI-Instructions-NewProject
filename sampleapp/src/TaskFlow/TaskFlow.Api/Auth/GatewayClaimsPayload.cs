// ═══════════════════════════════════════════════════════════════
// Pattern: Gateway claims payload — deserialized from X-Orig-Request header.
// The Gateway serializes user claims into this JSON structure;
// the API deserializes it to populate the downstream ClaimsPrincipal.
// ═══════════════════════════════════════════════════════════════

using System.Text.Json.Serialization;

namespace TaskFlow.Api.Auth;

/// <summary>
/// Pattern: Claims payload record — represents user identity forwarded by the Gateway.
/// Gateway extracts JWT claims → serializes to JSON → X-Orig-Request header.
/// API deserializes → adds to ClaimsPrincipal via GatewayClaimsTransformer.
/// </summary>
public sealed class GatewayClaimsPayload
{
    /// <summary>Subject identifier (JWT "sub" claim).</summary>
    public string? Sub { get; set; }

    /// <summary>User's email address.</summary>
    public string? Email { get; set; }

    /// <summary>User's display name.</summary>
    public string? Name { get; set; }

    /// <summary>Entra ID object identifier (JWT "oid" claim).</summary>
    public string? Oid { get; set; }

    /// <summary>Tenant identifier — determines which tenant the user belongs to.</summary>
    public string? UserTenantId { get; set; }

    /// <summary>User's application roles (e.g., "GlobalAdmin", "TenantAdmin").</summary>
    public string[]? UserRoles { get; set; }
}

/// <summary>
/// Pattern: Source-generated JSON serializer context — AOT-compatible, zero-reflection.
/// Specifies camelCase naming to match the Gateway's JSON output.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GatewayClaimsPayload))]
internal partial class GatewayClaimsJsonContext : JsonSerializerContext;
