// ═══════════════════════════════════════════════════════════════
// Pattern: Load Test Utility — HTTP client factory and auth helpers for NBomber scenarios.
// Provides a pre-configured HttpClient with base URL and optional bearer token.
//
// Usage: var client = Utility.CreateHttpClient(config);
//        var token  = await Utility.GetBearerTokenAsync(config);
// ═══════════════════════════════════════════════════════════════

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Test.Load;

/// <summary>
/// Pattern: Shared utility for load test scenarios — HTTP client + auth.
/// </summary>
public static class Utility
{
    /// <summary>
    /// Pattern: Create an HttpClient configured with base URL from appsettings.
    /// Optionally includes a bearer token for authenticated endpoints.
    /// </summary>
    public static HttpClient CreateHttpClient(IConfiguration config, string? bearerToken = null)
    {
        var baseUrl = config["BaseUrl"] ?? "https://localhost:7200";

        var handler = new HttpClientHandler
        {
            // Pattern: Accept self-signed certs for local development.
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl)
        };

        if (!string.IsNullOrEmpty(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        // Pattern: Standard headers for API consumption.
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("X-Tenant-Id",
            "00000000-0000-0000-0000-000000000099");

        return client;
    }

    /// <summary>
    /// Pattern: Acquire a bearer token from the Gateway auth endpoint.
    /// For load testing, typically a service-to-service / client credentials flow.
    /// </summary>
    public static async Task<string> GetBearerTokenAsync(IConfiguration config)
    {
        var tokenEndpoint = config["Auth:TokenEndpoint"]
            ?? "https://localhost:7200/auth/token";
        var clientId = config["Auth:ClientId"] ?? "load-test-client";
        var clientSecret = config["Auth:ClientSecret"] ?? "load-test-secret";

        using var httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        var response = await httpClient.PostAsJsonAsync(tokenEndpoint, new
        {
            client_id = clientId,
            client_secret = clientSecret,
            grant_type = "client_credentials"
        });

        if (!response.IsSuccessStatusCode)
        {
            // Pattern: Fallback — return a contrived token for demo/local testing.
            Console.WriteLine($"[WARN] Token request failed ({response.StatusCode}). Using contrived token.");
            return "load-test-contrived-token";
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return result?.AccessToken ?? "load-test-contrived-token";
    }

    // Pattern: Minimal token response DTO.
    private sealed record TokenResponse(string AccessToken);
}
