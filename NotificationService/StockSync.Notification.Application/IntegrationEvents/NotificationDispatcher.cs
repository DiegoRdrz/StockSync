using StockSync.Notification.Application.Channels;
using StockSync.Notification.Domain.Entities;
using StockSync.Notification.Domain.Repositories;

namespace StockSync.Notification.Application.IntegrationEvents;

public sealed class NotificationDispatcher
{
    public const string DefaultEmailRecipient = "alerts@stocksync.demo";
    public const string DefaultWhatsAppRecipient = "+15550100100";

    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationRecipientRepository _recipientRepository;
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationChannelSender> _senders;

    public NotificationDispatcher(
        INotificationRepository notificationRepository,
        INotificationRecipientRepository recipientRepository,
        IEnumerable<INotificationChannelSender> senders)
    {
        _notificationRepository = notificationRepository;
        _recipientRepository = recipientRepository;
        _senders = senders.ToDictionary(s => s.Channel);
    }

    public Task<IReadOnlyList<Domain.Entities.Notification>> DispatchOrderCreatedAsync(
        OrderCreatedEvent message,
        CancellationToken cancellationToken)
    {
        var subject = $"[StockSync] New order created ({message.OrderId})";
        var body =
            $"A new sales order '{message.OrderId}' was created for branch '{message.BranchId}' at {message.CreatedAt:O}.";

        return DispatchAsync(
            message.EventId,
            NotificationEventType.OrderCreated,
            message.BranchId,
            message.OrderId,
            subject,
            body,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Domain.Entities.Notification>> DispatchLowStockIfTriggeredAsync(
        StockMovementRecordedEvent message,
        CancellationToken cancellationToken)
    {
        if (!message.TriggersLowStockAlert)
            return [];

        var subject = $"[StockSync] Low stock alert: {message.ProductName} ({message.ProductSku})";
        var body =
            $"Product '{message.ProductName}' (SKU: {message.ProductSku}) at branch '{message.BranchId}' " +
            $"is below minimum stock. Current quantity: {message.NewQuantity}; minimum required: {message.MinimumStock}; " +
            $"shortage: {message.Shortage}.";

        return await DispatchAsync(
            message.EventId,
            NotificationEventType.LowStock,
            message.BranchId,
            message.StockId,
            subject,
            body,
            cancellationToken);
    }

    private async Task<IReadOnlyList<Domain.Entities.Notification>> DispatchAsync(
        Guid sourceEventId,
        NotificationEventType eventType,
        Guid branchId,
        Guid resourceId,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var activeRecipients = await _recipientRepository.ListActiveAsync(cancellationToken);
        var targets = ResolveTargets(activeRecipients);
        var created = new List<Domain.Entities.Notification>(targets.Count);

        foreach (var (channel, destination) in targets)
        {
            var notification = Domain.Entities.Notification.Create(
                sourceEventId,
                eventType,
                channel,
                destination,
                subject,
                body,
                branchId,
                resourceId);

            if (_senders.TryGetValue(channel, out var sender))
            {
                try
                {
                    await sender.SendAsync(destination, subject, body, cancellationToken);
                    notification.MarkSent();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    notification.MarkFailed(ex.Message);
                }
            }
            else
            {
                notification.MarkFailed($"No sender configured for channel '{channel}'.");
            }

            await _notificationRepository.AddAsync(notification, cancellationToken);
            created.Add(notification);
        }

        return created;
    }

    private static IReadOnlyList<(NotificationChannel Channel, string Destination)> ResolveTargets(
        IReadOnlyList<NotificationRecipient> recipients)
    {
        if (recipients.Count > 0)
            return recipients.Select(r => (r.Channel, r.Destination)).ToList();

        // Fallback destinations ensure both Email and WhatsApp notifications are emitted
        // even before a tenant customizes its recipient list.
        return
        [
            (NotificationChannel.Email, DefaultEmailRecipient),
            (NotificationChannel.WhatsApp, DefaultWhatsAppRecipient)
        ];
    }
}
