// ═══════════════════════════════════════════════════════════════
// Pattern: TokenService — acquires client credential tokens for downstream clusters.
// ConcurrentDictionary cache with expiry buffer prevents token storms.
// Each YARP cluster gets its own token (different scopes/audiences).
// ═══════════════════════════════════════════════════════════════

using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;

namespace TaskFlow.Gateway;

/// <summary>
/// Pattern: Service-to-service token acquisition with caching.
/// Gateway authenticates to downstream APIs using client credentials (Entra ID app registration).
/// Config: ServiceAuth:{clusterId} section with TenantId, ClientId, ClientSecret, Scope.
/// Cache invalidation: 5-minute buffer before actual expiry.
/// </summary>
public class TokenService(IConfiguration config, ILogger<TokenService> logger)
{
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expiry)> _cache = new();

    public async Task<string> GetAccessTokenAsync(string clusterId)
    {
        // Pattern: Return cached token if still valid (with 5-minute buffer).
        if (_cache.TryGetValue(clusterId, out var cached) && cached.Expiry > DateTimeOffset.UtcNow.AddMinutes(5))
            return cached.Token;

        // Pattern: Resolve per-cluster auth config from ServiceAuth:{clusterId} section.
        var section = config.GetSection($"ServiceAuth:{clusterId}");
        var tenantId = section["TenantId"];
        var clientId = section["ClientId"];
        var clientSecret = section["ClientSecret"];
        var scope = section["Scope"];

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) ||
            string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(scope))
        {
            logger.LogWarning("ServiceAuth config missing for cluster {ClusterId} — returning empty token", clusterId);
            return string.Empty;
        }

        // Pattern: ClientSecretCredential — Entra ID client credentials flow.
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var tokenResult = await credential.GetTokenAsync(new TokenRequestContext([scope]));

        _cache[clusterId] = (tokenResult.Token, tokenResult.ExpiresOn);

        logger.LogDebug("Acquired service token for cluster {ClusterId}, expires {Expiry}",
            clusterId, tokenResult.ExpiresOn);

        return tokenResult.Token;
    }
}
