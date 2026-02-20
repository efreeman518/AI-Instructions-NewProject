// ═══════════════════════════════════════════════════════════════
// Pattern: Provider Interfaces — each notification channel has its own interface.
// Channel-specific logic lives in provider implementations, NOT in NotificationService.
// Providers are registered conditionally based on configuration presence.
// ═══════════════════════════════════════════════════════════════

using Infrastructure.Notification.Model;

namespace Infrastructure.Notification.Providers;

/// <summary>
/// Pattern: Per-channel provider interface — Email.
/// Implementations: AzureEmailProvider (Azure Communication Services).
/// Registered only when "Notification:Email" config section exists.
/// </summary>
public interface IEmailProvider
{
    /// <summary>Send a single email message.</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>Send a batch of email messages with throttling.</summary>
    Task<IReadOnlyList<bool>> SendBatchAsync(IEnumerable<EmailMessage> messages, CancellationToken ct = default);
}

/// <summary>
/// Pattern: Per-channel provider interface — SMS.
/// Implementations: TwilioSmsProvider.
/// Registered only when "Notification:Sms" config section exists.
/// </summary>
public interface ISmsProvider
{
    /// <summary>Send a single SMS message.</summary>
    Task<bool> SendAsync(SmsMessage message, CancellationToken ct = default);

    /// <summary>Send a batch of SMS messages with throttling.</summary>
    Task<IReadOnlyList<bool>> SendBatchAsync(IEnumerable<SmsMessage> messages, CancellationToken ct = default);
}
