using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StockSync.Notification.Application.Channels;
using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Infrastructure.Senders;

public sealed record WhatsAppOptions(string? WebhookUrl);

public sealed class WhatsAppNotificationSender : INotificationChannelSender
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppNotificationSender> _logger;

    public WhatsAppNotificationSender(
        HttpClient httpClient,
        WhatsAppOptions options,
        ILogger<WhatsAppNotificationSender> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.WhatsApp;

    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogInformation(
                "WhatsApp notification dispatched (simulated) to {Recipient}: {Subject}",
                recipient,
                subject);
            return;
        }

        using var response = await _httpClient.PostAsJsonAsync(
            _options.WebhookUrl,
            new { to = recipient, subject, message = body },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        _logger.LogInformation("WhatsApp notification sent via webhook to {Recipient}: {Subject}", recipient, subject);
    }
}
