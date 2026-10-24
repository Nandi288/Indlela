using System.Net;
using System.Net.Mail;

namespace Indlela.Services.Email;

/// <summary>Transactional email (UC-01 verification, UC-16 reminders, UC-20 nudges). Keep bodies under 50 KB.</summary>
public interface IAppEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>SMTP settings, from user secrets (development) or the host's settings (production). Never in appsettings.json.</summary>
public class SmtpOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    /// <summary>The Gmail address that sends the emails.</summary>
    public string Username { get; set; } = string.Empty;
    /// <summary>A Gmail app password (16 letters), not the normal Gmail password.</summary>
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Indlela";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>Sends real email over SMTP with STARTTLS (works with Gmail app passwords). Built into .NET, no extra package.</summary>
public sealed class SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger) : IAppEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var from = string.IsNullOrWhiteSpace(options.FromAddress) ? options.Username : options.FromAddress;
        using var message = new MailMessage
        {
            From = new MailAddress(from, options.FromName),
            Subject = subject,
            Body = EmailTemplate.Wrap(subject, htmlBody),
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(options.Username, options.Password)
        };

        try
        {
            await client.SendMailAsync(message, ct);
            logger.LogInformation("Email sent to {To}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Never crash a sign-up because email failed; the learner can ask for the link again by logging in.
            logger.LogError(ex, "Email to {To} failed: {Subject}", toEmail, subject);
        }
    }
}

/// <summary>Development fallback when no SMTP settings exist: writes the email to the log instead of sending it.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IAppEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogWarning("EMAIL NOT SENT (no Email:Smtp settings) to {To} — {Subject}\n{Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>A simple, email-client-safe layout in Indlela's green and gold (inline styles only).</summary>
public static class EmailTemplate
{
    public static string Wrap(string title, string bodyHtml) => $"""
        <!DOCTYPE html>
        <html><body style="margin:0;padding:0;background:#F4F1E8;font-family:Arial,Helvetica,sans-serif;color:#17201B">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#F4F1E8;padding:24px 12px">
            <tr><td align="center">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:14px;overflow:hidden">
                <tr><td style="background:#0A2A20;padding:20px 28px">
                  <span style="font-family:Georgia,serif;font-size:26px;font-weight:bold;color:#E8B04B">Indlela</span>
                </td></tr>
                <tr><td style="padding:28px;font-size:16px;line-height:1.6">
                  <h1 style="font-family:Georgia,serif;font-size:22px;margin:0 0 16px;color:#0A2A20">{System.Net.WebUtility.HtmlEncode(title)}</h1>
                  {bodyHtml}
                </td></tr>
                <tr><td style="padding:16px 28px;background:#F7F5EE;font-size:12px;color:#5B6470">
                  You're receiving this because you created an Indlela account. If that wasn't you, you can ignore this email.
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body></html>
        """;
}
