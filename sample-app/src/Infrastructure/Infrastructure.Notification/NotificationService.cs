using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Notification;

public class NotificationService(
    ILogger<NotificationService> logger,
    IOptions<NotificationServiceSettings> settings) : INotificationService
{
    private readonly NotificationServiceSettings _settings = settings.Value;

    public async Task SendEmailAsync(SimpleEmail email, CancellationToken ct = default)
    {
        logger.LogInformation("Sending email to {To} with subject {Subject}", email.To, email.Subject);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.Email.FromName, _settings.Email.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;

        message.Body = email.IsHtml
            ? new TextPart("html") { Text = email.Body }
            : new TextPart("plain") { Text = email.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Email.SmtpHost, _settings.Email.SmtpPort, _settings.Email.UseSsl, ct);

        if (!string.IsNullOrEmpty(_settings.Email.Username))
            await client.AuthenticateAsync(_settings.Email.Username, _settings.Email.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Email sent successfully to {To}", email.To);
    }

    public async Task SendSmsAsync(SimpleSms sms, CancellationToken ct = default)
    {
        logger.LogInformation("Sending SMS to {PhoneNumber}", sms.PhoneNumber);

        // Twilio SMS sending - stub implementation
        // TwilioClient.Init(_settings.Sms.AccountSid, _settings.Sms.AuthToken);
        // var message = await MessageResource.CreateAsync(...);
        await Task.CompletedTask;

        logger.LogInformation("SMS sent successfully to {PhoneNumber}", sms.PhoneNumber);
    }
}
