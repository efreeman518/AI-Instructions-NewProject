// ═══════════════════════════════════════════════════════════════
// Pattern: Health check — SQL managed identity token acquisition.
// Verifies that the app can acquire an AAD token for Azure SQL access.
// This catches managed identity misconfigurations early.
// ═══════════════════════════════════════════════════════════════

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TaskFlow.Api.HealthChecks;

/// <summary>
/// Pattern: Custom IHealthCheck for AAD token acquisition.
/// In Azure-hosted environments, database access uses managed identity tokens.
/// This check verifies the identity can be resolved and a token acquired
/// for the "https://database.windows.net/.default" scope.
/// Skipped when using SQL Server authentication (non-Azure).
/// </summary>
public class SqlTokenHealthCheck(
    IConfiguration config,
    ILogger<SqlTokenHealthCheck> logger) : IHealthCheck
{
    private const string AzureSqlScope = "https://database.windows.net/.default";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = config.GetConnectionString("TaskFlowDbContextTrxn") ?? "";

            // Pattern: Skip token check for local SQL Server (contains "User Id" or "Integrated Security").
            if (connectionString.Contains("User Id", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase))
            {
                return HealthCheckResult.Healthy("SQL authentication mode — token check skipped.");
            }

            // Pattern: Attempt DefaultAzureCredential token acquisition.
            // DefaultAzureCredential chains: Managed Identity → Visual Studio → Azure CLI → etc.
            var credential = new DefaultAzureCredential();
            var tokenResult = await credential.GetTokenAsync(
                new TokenRequestContext([AzureSqlScope]),
                cancellationToken);

            if (!string.IsNullOrEmpty(tokenResult.Token))
            {
                logger.LogDebug("SQL token health check passed. Expires={Expiry}", tokenResult.ExpiresOn);
                return HealthCheckResult.Healthy(
                    $"AAD token acquired for Azure SQL. Expires: {tokenResult.ExpiresOn:u}");
            }

            return HealthCheckResult.Unhealthy("AAD token was empty.");
        }
        catch (CredentialUnavailableException ex)
        {
            logger.LogError(ex, "SQL token health check: no credential available");
            return HealthCheckResult.Unhealthy(
                "Managed identity credential unavailable. Ensure the app has a system-assigned or user-assigned managed identity.", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SQL token health check failed");
            return HealthCheckResult.Unhealthy("SQL token health check failed.", ex);
        }
    }
}
