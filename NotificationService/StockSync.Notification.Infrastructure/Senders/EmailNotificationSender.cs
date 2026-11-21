using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using StockSync.Notification.Application.Channels;
using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Infrastructure.Senders;

public sealed record SmtpOptions(
    string? Host,
    int Port = 587,
    string FromAddress = "no-reply@stocksync.demo",
    string? Username = null,
    string? Password = null,
    bool EnableSsl = false);

public sealed class EmailNotificationSender : INotificationChannelSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailNotificationSender> _logger;

    public EmailNotificationSender(SmtpOptions options, ILogger<EmailNotificationSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogInformation(
                "Email notification dispatched (simulated) to {Recipient}: {Subject}",
                recipient,
                subject);
            return;
        }

        using var message = new MailMessage(_options.FromAddress, recipient, subject, body);
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Email notification sent via SMTP to {Recipient}: {Subject}", recipient, subject);
    }
}
