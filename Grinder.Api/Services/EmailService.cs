using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Grinder.Api.Services;

/// <summary>
/// Defines operations for sending emails using the MailKit library.
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body);
}

/// <summary>
/// A service for sending emails using the MailKit library.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly EmailSettings _settings;
    public EmailService(IConfiguration configuration, IOptions<EmailSettings> settings)
    {
        _configuration = configuration;
        _settings = settings.Value;
    }

    /// <summary>
    /// Sends an email to the specificed recepient with a given subject line and body content.
    /// </summary>
    /// <param name="toEmail">
    /// The email address of the recipient.
    /// </param>
    /// <param name="subject">
    /// The subject line of the email.
    /// </param>
    /// <param name="body">
    /// The content of the email.
    /// </param>
    /// <remarks>
    /// This method retrieves configuration settings from appsettings.json, including the send email and SMTP server details.
    /// It creates a MimeMessage using the settings and parameters which define the email content and details.
    /// Then it connects to a SMTP server using MailKit's SmtpClient, authenticates it using host defined credentials, sends the email and disconnects.
    /// </remarks>
    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var fromName = _settings.FromName;
        var fromEmail = _settings.FromEmail;
        var smtpServer = _settings.SmtpServer;
        var smtpPort = _settings.SmtpPort;
        var username = _settings.Username;
        var password = _settings.Password;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = body
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(username, password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
