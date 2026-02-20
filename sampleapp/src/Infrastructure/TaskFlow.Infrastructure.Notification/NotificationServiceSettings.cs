// ═══════════════════════════════════════════════════════════════
// Pattern: Settings POCO — lightweight settings class for notification configuration.
// Used alongside NotificationOptions for provider-specific runtime settings.
// ═══════════════════════════════════════════════════════════════

namespace Infrastructure.Notification;

/// <summary>
/// Pattern: Settings POCO bound from configuration.
/// Provides runtime-accessible notification settings without options pattern overhead.
/// </summary>
public class NotificationServiceSettings
{
    /// <summary>Whether the notification system is globally enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether to disable actual sending (log-only mode for dev/test).</summary>
    public bool DryRunMode { get; set; }

    /// <summary>Global timeout for individual send operations in seconds.</summary>
    public int SendTimeoutSeconds { get; set; } = 30;

    /// <summary>Whether to throw on send failure or silently degrade.</summary>
    public bool ThrowOnFailure { get; set; }

    /// <summary>Default "from" address for email (can be overridden per-message).</summary>
    public string DefaultFromEmail { get; set; } = "noreply@example.com";

    /// <summary>Default "from" phone for SMS (can be overridden per-message).</summary>
    public string? DefaultFromPhone { get; set; }
}
