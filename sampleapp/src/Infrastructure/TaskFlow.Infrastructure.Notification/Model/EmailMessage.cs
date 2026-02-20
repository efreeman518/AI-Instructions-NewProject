// ═══════════════════════════════════════════════════════════════
// Pattern: Notification Model — plain DTO for email message delivery.
// These models have no dependency on Domain or Application layers.
// They are passed to providers for actual delivery.
// ═══════════════════════════════════════════════════════════════

namespace Infrastructure.Notification.Model;

/// <summary>
/// Pattern: Plain DTO for email notification — no domain coupling.
/// Used by IEmailProvider to send messages via SMTP or Azure Communication Services.
/// </summary>
public class EmailMessage
{
    /// <summary>Recipient email address.</summary>
    public required string To { get; init; }

    /// <summary>Optional CC recipients.</summary>
    public IReadOnlyList<string> Cc { get; init; } = [];

    /// <summary>Optional BCC recipients.</summary>
    public IReadOnlyList<string> Bcc { get; init; } = [];

    /// <summary>Email subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Plain text body (fallback).</summary>
    public string? PlainTextBody { get; init; }

    /// <summary>HTML body (preferred).</summary>
    public string? HtmlBody { get; init; }

    /// <summary>Optional sender override (falls back to configured default).</summary>
    public string? FromAddress { get; init; }

    /// <summary>Optional sender display name override.</summary>
    public string? FromName { get; init; }

    /// <summary>Optional reply-to address.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>Custom headers for tracking/correlation.</summary>
    public Dictionary<string, string> Headers { get; init; } = [];
}
