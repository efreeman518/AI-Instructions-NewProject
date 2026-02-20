// ═══════════════════════════════════════════════════════════════
// Pattern: Custom exception for notification failures.
// Wraps provider-specific exceptions with channel context.
// ═══════════════════════════════════════════════════════════════

namespace Infrastructure.Notification.Exceptions;

/// <summary>
/// Pattern: Domain-specific exception for notification delivery failures.
/// Captures channel type and provider details for diagnostics.
/// </summary>
public class NotificationException : Exception
{
    /// <summary>The notification channel that failed (Email, Sms, WebPush, AppPush).</summary>
    public string Channel { get; }

    /// <summary>The provider implementation that threw (e.g., "AzureCommunicationServices", "Twilio").</summary>
    public string? Provider { get; }

    public NotificationException(string channel, string message)
        : base(message)
    {
        Channel = channel;
    }

    public NotificationException(string channel, string message, Exception innerException)
        : base(message, innerException)
    {
        Channel = channel;
    }

    public NotificationException(string channel, string provider, string message, Exception innerException)
        : base(message, innerException)
    {
        Channel = channel;
        Provider = provider;
    }
}
