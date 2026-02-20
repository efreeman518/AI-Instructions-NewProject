// ═══════════════════════════════════════════════════════════════
// Pattern: NotificationService — implements INotificationService from Application.MessageHandlers.
// Orchestrates domain-specific notification methods by delegating to per-channel providers.
// Uses graceful degradation: if a provider is not configured (null), logs a warning and returns.
// Optional providers are injected as nullable constructor parameters.
// ═══════════════════════════════════════════════════════════════

using Application.MessageHandlers;
using Infrastructure.Notification.Model;
using Infrastructure.Notification.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notification;

/// <summary>
/// Pattern: Unified notification orchestrator — bridges domain-specific notification
/// intents (assignment, reminder, overdue) to channel-specific providers (email, SMS).
/// 
/// Graceful degradation: providers are optional (nullable). If not configured,
/// the service logs a warning and returns without throwing.
/// 
/// Implements INotificationService defined in Application.MessageHandlers
/// (consumed by TodoItemAssignedEventHandler, etc.).
/// </summary>
public class NotificationService(
    ILogger<NotificationService> logger,
    IOptions<NotificationServiceSettings> settings,
    IEmailProvider? emailProvider = null,
    ISmsProvider? smsProvider = null) : INotificationService
{
    private readonly NotificationServiceSettings _settings = settings.Value;

    // ═══════════════════════════════════════════════════════════════
    // Domain-Specific Notification Methods (from INotificationService)
    // Each method maps a domain intent to one or more channel sends.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pattern: Domain-to-channel mapping — assignment notification sends an email.
    /// Called by TodoItemAssignedEventHandler when a TodoItem is reassigned.
    /// </summary>
    public async Task SendAssignmentNotificationAsync(
        Guid userId, string todoItemTitle, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            logger.LogDebug("Notifications disabled, skipping assignment notification for {UserId}", userId);
            return;
        }

        logger.LogInformation("Sending assignment notification for TodoItem '{Title}' to user {UserId}",
            todoItemTitle, userId);

        // Pattern: Map domain intent to email message DTO.
        var email = new EmailMessage
        {
            To = $"{userId}@taskflow.example.com", // In production, resolve from user profile service
            Subject = $"You've been assigned: {todoItemTitle}",
            HtmlBody = $"""
                <h2>New Task Assignment</h2>
                <p>You have been assigned to the task: <strong>{todoItemTitle}</strong></p>
                <p>Please review and update the task status accordingly.</p>
                """,
            PlainTextBody = $"You have been assigned to the task: {todoItemTitle}"
        };

        await SendEmailInternalAsync(email, ct);
    }

    /// <summary>
    /// Pattern: Reminder notification — sends both email and SMS for high-priority reminders.
    /// Called by ReminderService when a reminder comes due.
    /// </summary>
    public async Task SendReminderNotificationAsync(
        Guid userId, string message, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            logger.LogDebug("Notifications disabled, skipping reminder for {UserId}", userId);
            return;
        }

        logger.LogInformation("Sending reminder notification to user {UserId}", userId);

        // Pattern: Multi-channel send — email + SMS for reminders.
        var email = new EmailMessage
        {
            To = $"{userId}@taskflow.example.com",
            Subject = "TaskFlow Reminder",
            HtmlBody = $"<p>{message}</p>",
            PlainTextBody = message
        };

        var sms = new SmsMessage
        {
            To = "+15551234567", // In production, resolve from user profile service
            Body = $"TaskFlow Reminder: {message}"
        };

        // Pattern: Fire both channels — errors in one don't block the other.
        await SendEmailInternalAsync(email, ct);
        await SendSmsInternalAsync(sms, ct);
    }

    /// <summary>
    /// Pattern: Overdue notification — sends email alert when a TodoItem passes its due date.
    /// </summary>
    public async Task SendOverdueNotificationAsync(
        Guid userId, string todoItemTitle, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            logger.LogDebug("Notifications disabled, skipping overdue notification for {UserId}", userId);
            return;
        }

        logger.LogInformation("Sending overdue notification for TodoItem '{Title}' to user {UserId}",
            todoItemTitle, userId);

        var email = new EmailMessage
        {
            To = $"{userId}@taskflow.example.com",
            Subject = $"Overdue: {todoItemTitle}",
            HtmlBody = $"""
                <h2>Task Overdue</h2>
                <p>The following task is now overdue: <strong>{todoItemTitle}</strong></p>
                <p>Please update the task or request an extension.</p>
                """,
            PlainTextBody = $"Task overdue: {todoItemTitle}. Please update or request an extension."
        };

        await SendEmailInternalAsync(email, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // Internal Channel Dispatchers
    // Pattern: Graceful degradation — null provider = log warning + return.
    // DryRunMode = log the message but don't actually send.
    // ═══════════════════════════════════════════════════════════════

    private async Task SendEmailInternalAsync(EmailMessage message, CancellationToken ct)
    {
        if (emailProvider is null)
        {
            logger.LogWarning("Email provider not configured — skipping email to {To}", message.To);
            return;
        }

        if (_settings.DryRunMode)
        {
            logger.LogInformation("[DRY RUN] Would send email to {To}: {Subject}", message.To, message.Subject);
            return;
        }

        try
        {
            await emailProvider.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}", message.To);
            if (_settings.ThrowOnFailure) throw;
        }
    }

    private async Task SendSmsInternalAsync(SmsMessage message, CancellationToken ct)
    {
        if (smsProvider is null)
        {
            logger.LogWarning("SMS provider not configured — skipping SMS to {To}", message.To);
            return;
        }

        if (_settings.DryRunMode)
        {
            logger.LogInformation("[DRY RUN] Would send SMS to {To}: {Body}", message.To, message.Body);
            return;
        }

        try
        {
            await smsProvider.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS to {To}", message.To);
            if (_settings.ThrowOnFailure) throw;
        }
    }
}
