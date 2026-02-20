// ═══════════════════════════════════════════════════════════════
// Pattern: SMS Provider — Twilio implementation.
// Uses Twilio SDK for SMS/MMS delivery.
// Registered only when "Notification:Sms" config section exists.
// Credentials (AccountSid, AuthToken) come from Key Vault, never hardcoded.
// ═══════════════════════════════════════════════════════════════

using Infrastructure.Notification.Configuration;
using Infrastructure.Notification.Exceptions;
using Infrastructure.Notification.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Infrastructure.Notification.Providers.Sms;

/// <summary>
/// Pattern: Provider implementation — Twilio SMS.
/// Wraps the Twilio SDK with logging, error handling, and batch throttling.
/// </summary>
public class TwilioSmsProvider : ISmsProvider
{
    private readonly ILogger<TwilioSmsProvider> _logger;
    private readonly SmsOptions _smsConfig;
    private readonly BatchThrottlingOptions _throttling;

    public TwilioSmsProvider(
        ILogger<TwilioSmsProvider> logger,
        IOptions<NotificationOptions> options)
    {
        _logger = logger;
        _smsConfig = options.Value.Sms!;
        _throttling = options.Value.BatchThrottling;

        // Pattern: Initialize Twilio client on construction — credentials from Key Vault.
        TwilioClient.Init(_smsConfig.AccountSid, _smsConfig.AuthToken);
    }

    // ═══════════════════════════════════════════════════════════════
    // Single Send
    // ═══════════════════════════════════════════════════════════════

    public async Task<bool> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Sending SMS to {To} via Twilio", message.To);

            var result = await MessageResource.CreateAsync(
                to: new PhoneNumber(message.To),
                from: new PhoneNumber(message.FromNumber ?? _smsConfig.FromNumber),
                body: message.Body);

            _logger.LogInformation("SMS sent to {To}, SID: {Sid}, Status: {Status}",
                message.To, result.Sid, result.Status);

            return result.Status != MessageResource.StatusEnum.Failed
                && result.Status != MessageResource.StatusEnum.Undelivered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {To}", message.To);
            throw new NotificationException("Sms", "Twilio",
                $"Failed to send SMS to {message.To}", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Batch Send
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pattern: Batch throttling with Parallel.ForEachAsync — respects Twilio rate limits.
    /// </summary>
    public async Task<IReadOnlyList<bool>> SendBatchAsync(
        IEnumerable<SmsMessage> messages, CancellationToken ct = default)
    {
        var messageList = messages.ToList();
        _logger.LogInformation("Sending batch of {Count} SMS messages", messageList.Count);

        var results = new bool[messageList.Count];

        await Parallel.ForEachAsync(
            messageList.Select((msg, idx) => (msg, idx)),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _throttling.MaxConcurrency,
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
        _logger.LogInformation("Batch SMS complete: {Success}/{Total} succeeded",
            successCount, messageList.Count);

        return results;
    }
}
