namespace Indlela.Services.Email;

/// <summary>Transactional email (UC-01 verification, UC-16 reminders, UC-20 nudges). Keep bodies under 50 KB.</summary>
public interface IAppEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>Development sender: writes the email to the log instead of sending it. Replace with SendGrid/ACS.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IAppEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("EMAIL (not sent) to {To} — {Subject}\n{Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
