// ═══════════════════════════════════════════════════════════════
// Pattern: Health check — OIDC metadata endpoint.
// Verifies the identity provider (Entra ID / B2C) is reachable
// by fetching the OpenID Connect discovery document.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TaskFlow.Api.HealthChecks;

/// <summary>
/// Pattern: Custom IHealthCheck for OIDC provider connectivity.
/// Constructs the well-known OIDC metadata URL from config and GETs it.
/// A 200 response means the identity provider is healthy.
/// Config path: "AzureAd:Instance", "AzureAd:TenantId" (or "AzureAd:Domain" for B2C).
/// </summary>
public class AuthOidcHealthCheck(
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<AuthOidcHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadataUrl = BuildOidcMetadataUrl();
            if (string.IsNullOrEmpty(metadataUrl))
            {
                return HealthCheckResult.Degraded("OIDC metadata URL could not be constructed from config.");
            }

            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(metadataUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogDebug("OIDC metadata endpoint healthy: {Url}", metadataUrl);
                return HealthCheckResult.Healthy($"OIDC metadata endpoint is reachable: {metadataUrl}");
            }

            logger.LogWarning("OIDC metadata endpoint returned {StatusCode}: {Url}",
                response.StatusCode, metadataUrl);
            return HealthCheckResult.Unhealthy(
                $"OIDC metadata endpoint returned {response.StatusCode}.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OIDC health check failed");
            return HealthCheckResult.Unhealthy("OIDC health check failed.", ex);
        }
    }

    /// <summary>
    /// Pattern: Build OIDC discovery URL from config — supports both Entra ID and B2C.
    /// Entra ID: {Instance}{TenantId}/v2.0/.well-known/openid-configuration
    /// B2C: {Instance}{Domain}/{SignUpSignInPolicyId}/v2.0/.well-known/openid-configuration
    /// </summary>
    private string? BuildOidcMetadataUrl()
    {
        var section = config.GetSection("AzureAd");
        var instance = section["Instance"]?.TrimEnd('/');
        var tenantId = section["TenantId"];
        var domain = section["Domain"];
        var policyId = section["SignUpSignInPolicyId"];

        // Pattern: B2C uses Domain + PolicyId.
        if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(policyId))
        {
            return $"{instance}/{domain}/{policyId}/v2.0/.well-known/openid-configuration";
        }

        // Pattern: Standard Entra ID uses TenantId.
        if (!string.IsNullOrEmpty(tenantId))
        {
            return $"{instance}/{tenantId}/v2.0/.well-known/openid-configuration";
        }

        return null;
    }
}
