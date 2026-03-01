namespace Infrastructure.Notification;

public interface INotificationService
{
    Task SendEmailAsync(SimpleEmail email, CancellationToken ct = default);
    Task SendSmsAsync(SimpleSms sms, CancellationToken ct = default);
}

public record SimpleEmail(string To, string Subject, string Body, bool IsHtml = false);
public record SimpleSms(string PhoneNumber, string Message);
