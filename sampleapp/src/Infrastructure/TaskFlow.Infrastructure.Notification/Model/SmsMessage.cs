// ═══════════════════════════════════════════════════════════════
// Pattern: Notification Model — plain DTO for SMS message delivery.
// No domain coupling — just delivery data.
// ═══════════════════════════════════════════════════════════════

namespace Infrastructure.Notification.Model;

/// <summary>
/// Pattern: Plain DTO for SMS notification.
/// Used by ISmsProvider to send messages via Twilio or Azure Communication Services.
/// </summary>
public class SmsMessage
{
    /// <summary>Recipient phone number in E.164 format (e.g., +15551234567).</summary>
    public required string To { get; init; }

    /// <summary>SMS message body (max 1600 chars for concatenated SMS).</summary>
    public required string Body { get; init; }

    /// <summary>Optional sender phone number override (falls back to configured default).</summary>
    public string? FromNumber { get; init; }

    /// <summary>Optional metadata for tracking/correlation.</summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
}
