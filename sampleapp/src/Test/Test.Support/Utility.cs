// ═══════════════════════════════════════════════════════════════
// Pattern: Test utility class — shared helpers for all test projects.
// BuildConfiguration: layered config builder matching production hierarchy.
// RandomString: generates random alphanumeric strings for test data uniqueness.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Configuration;

namespace Test.Support;

/// <summary>
/// Pattern: Static utility class for test infrastructure.
/// Provides configuration builders and data generators shared across all test projects.
/// </summary>
public static class Utility
{
    /// <summary>
    /// Pattern: BuildConfiguration — mirrors the production config hierarchy:
    /// appsettings.json → appsettings.{env}.json → environment variables → user secrets.
    /// Returns IConfigurationBuilder (not IConfigurationRoot) so callers can add
    /// additional sources (e.g., AddUserSecrets) before calling .Build().
    /// </summary>
    /// <param name="path">
    /// Primary JSON file path. Defaults to "appsettings.json" which resolves from
    /// the test project's output directory (bin/Debug/net10.0/).
    /// Pass "appsettings-test.json" for integration tests.
    /// Pass null to skip JSON file loading entirely.
    /// </param>
    /// <param name="includeEnvironmentVars">
    /// Whether to include environment variables in the configuration.
    /// Default true — allows CI/CD to override settings via env vars.
    /// </param>
    public static IConfigurationBuilder BuildConfiguration(
        string? path = "appsettings.json", bool includeEnvironmentVars = true)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory());

        // Pattern: Base appsettings — always loaded if path is specified.
        if (path is not null)
            builder.AddJsonFile(path, optional: true);

        // Pattern: Environment variables — highest priority non-secret source.
        if (includeEnvironmentVars)
            builder.AddEnvironmentVariables();

        // Pattern: Environment-specific overlay — matches ASPNETCORE_ENVIRONMENT.
        // Build the config so far to read the environment name, then add the overlay.
        var config = builder.Build();
        var env = config.GetValue("ASPNETCORE_ENVIRONMENT", "development")!.ToLower();
        builder.AddJsonFile($"appsettings.{env}.json", optional: true);

        return builder;
    }

    /// <summary>
    /// Pattern: RandomString — generates a random alphanumeric string of given length.
    /// Uses Span{char} + Random.Shared for allocation-efficient, thread-safe generation.
    /// Useful for creating unique test entity names to avoid collisions in parallel tests.
    /// </summary>
    /// <param name="length">Length of the random string to generate.</param>
    /// <returns>A random lowercase alphanumeric string.</returns>
    public static string RandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        Span<char> result = stackalloc char[length];
        for (var i = 0; i < length; i++)
            result[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(result);
    }
}
