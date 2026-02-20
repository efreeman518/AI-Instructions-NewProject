// ═══════════════════════════════════════════════════════════════
// Pattern: Strongly-typed configuration bound from appsettings "Notification" section.
// Each channel has its own options class. Missing config = provider not registered.
// ═══════════════════════════════════════════════════════════════

namespace Infrastructure.Notification.Configuration;

/// <summary>
/// Pattern: Root configuration options for the notification system.
/// Bound from "Notification" section in appsettings.json.
/// </summary>
public class NotificationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notification";

    /// <summary>Email provider configuration. Null if email not configured.</summary>
    public EmailOptions? Email { get; set; }

    /// <summary>SMS provider configuration. Null if SMS not configured.</summary>
    public SmsOptions? Sms { get; set; }

    /// <summary>Batch throttling configuration for bulk sends.</summary>
    public BatchThrottlingOptions BatchThrottling { get; set; } = new();
}

/// <summary>
/// Pattern: Email provider configuration — supports Azure Communication Services.
/// Credentials come from Key Vault, never hardcoded in appsettings.
/// </summary>
public class EmailOptions
{
    /// <summary>Provider type: "AzureCommunicationServices" or "Smtp".</summary>
    public string Provider { get; set; } = "AzureCommunicationServices";

    /// <summary>Azure Communication Services connection string (from Key Vault).</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Default sender email address.</summary>
    public string FromAddress { get; set; } = "noreply@example.com";

    /// <summary>Default sender display name.</summary>
    public string FromName { get; set; } = "TaskFlow Notifications";
}

/// <summary>
/// Pattern: SMS provider configuration — supports Twilio.
/// Credentials come from Key Vault, never hardcoded in appsettings.
/// </summary>
public class SmsOptions
{
    /// <summary>Provider type: "Twilio".</summary>
    public string Provider { get; set; } = "Twilio";

    /// <summary>Twilio Account SID (from Key Vault).</summary>
    public string? AccountSid { get; set; }

    /// <summary>Twilio Auth Token (from Key Vault).</summary>
    public string? AuthToken { get; set; }

    /// <summary>Default sender phone number in E.164 format.</summary>
    public string? FromNumber { get; set; }
}

/// <summary>
/// Pattern: Batch throttling — controls concurrency for bulk notification sends.
/// Prevents provider rate-limit violations.
/// </summary>
public class BatchThrottlingOptions
{
    /// <summary>Maximum concurrent sends per batch operation.</summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>Delay between batch pages in milliseconds.</summary>
    public int DelayBetweenBatchesMs { get; set; } = 100;
}
