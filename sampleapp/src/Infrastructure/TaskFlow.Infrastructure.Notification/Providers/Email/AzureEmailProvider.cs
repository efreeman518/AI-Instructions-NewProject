// ═══════════════════════════════════════════════════════════════
// Pattern: Email Provider — Azure Communication Services implementation.
// Uses Azure.Communication.Email SDK for reliable transactional email delivery.
// Registered only when "Notification:Email" config section exists.
// Credentials come from Key Vault via managed identity, never hardcoded.
// ═══════════════════════════════════════════════════════════════

using Azure.Communication.Email;
using Infrastructure.Notification.Configuration;
using Infrastructure.Notification.Exceptions;
using Infrastructure.Notification.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notification.Providers.Email;

/// <summary>
/// Pattern: Provider implementation — Azure Communication Services email.
/// Wraps the Azure SDK with logging, error handling, and batch throttling.
/// </summary>
public class AzureEmailProvider(
    ILogger<AzureEmailProvider> logger,
    IOptions<NotificationOptions> options) : IEmailProvider
{
    // Pattern: Lazy initialization — client created on first use with connection string from config.
    private readonly Lazy<EmailClient> _client = new(() =>
        new EmailClient(options.Value.Email!.ConnectionString));

    private EmailOptions EmailConfig => options.Value.Email!;
    private BatchThrottlingOptions Throttling => options.Value.BatchThrottling;

    // ═══════════════════════════════════════════════════════════════
    // Single Send
    // ═══════════════════════════════════════════════════════════════

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        try
        {
            logger.LogDebug("Sending email to {To} via Azure Communication Services", message.To);

            // Pattern: Build Azure SDK email content from our plain DTO.
            var emailContent = new EmailContent(message.Subject);
            if (!string.IsNullOrWhiteSpace(message.HtmlBody))
                emailContent.Html = message.HtmlBody;
            if (!string.IsNullOrWhiteSpace(message.PlainTextBody))
                emailContent.PlainText = message.PlainTextBody;

            var emailMessage = new Azure.Communication.Email.EmailMessage(
                senderAddress: message.FromAddress ?? EmailConfig.FromAddress,
                recipientAddress: message.To,
                content: emailContent);

            // Pattern: Use WaitUntil.Started for fire-and-forget, WaitUntil.Completed for confirmation.
            var operation = await _client.Value.SendAsync(
                Azure.WaitUntil.Started, emailMessage, ct);

            logger.LogInformation("Email sent to {To}, OperationId: {OperationId}",
                message.To, operation.Id);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}", message.To);
            throw new NotificationException("Email", "AzureCommunicationServices",
                $"Failed to send email to {message.To}", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Batch Send
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pattern: Batch throttling — uses SemaphoreSlim to respect MaxConcurrency
    /// and prevent Azure Communication Services rate-limit violations.
    /// </summary>
    public async Task<IReadOnlyList<bool>> SendBatchAsync(
        IEnumerable<EmailMessage> messages, CancellationToken ct = default)
    {
        var messageList = messages.ToList();
        logger.LogInformation("Sending batch of {Count} emails", messageList.Count);

        // Pattern: Parallel.ForEachAsync with throttling via MaxDegreeOfParallelism.
        var results = new bool[messageList.Count];

        await Parallel.ForEachAsync(
            messageList.Select((msg, idx) => (msg, idx)),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Throttling.MaxConcurrency,
                CancellationToken = ct
            },
            async (item, token) =>
            {
                try
                {
                    results[item.idx] = await SendAsync(item.msg, token);
                }
                catch (NotificationException)
                {
                    results[item.idx] = false;
                }
            });

        var successCount = results.Count(r => r);
        logger.LogInformation("Batch email complete: {Success}/{Total} succeeded",
            successCount, messageList.Count);

        return results;
    }
}
