using StockSync.Notification.Application.Channels;
using StockSync.Notification.Application.IntegrationEvents;
using StockSync.Notification.Domain.Entities;
using StockSync.Notification.Domain.Repositories;

namespace StockSync.Notification.UnitTests.Application;

public class NotificationDispatcherTests
{
    [Fact]
    public async Task DispatchOrderCreated_UsesConfiguredRecipientsAcrossChannels()
    {
        var notifications = new FakeNotificationRepository();
        var recipients = new FakeRecipientRepository(
        [
            NotificationRecipient.Create("Owner Email", NotificationChannel.Email, "owner@stocksync.demo"),
            NotificationRecipient.Create("Owner WhatsApp", NotificationChannel.WhatsApp, "+573001112233")
        ]);
        var emailSender = new FakeSender(NotificationChannel.Email);
        var whatsAppSender = new FakeSender(NotificationChannel.WhatsApp);
        var dispatcher = new NotificationDispatcher(notifications, recipients, [emailSender, whatsAppSender]);

        var orderEvent = new OrderCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        var created = await dispatcher.DispatchOrderCreatedAsync(orderEvent, CancellationToken.None);

        Assert.Equal(2, created.Count);
        Assert.All(created, n => Assert.Equal(NotificationStatus.Sent, n.Status));
        Assert.Single(emailSender.SentMessages);
        Assert.Single(whatsAppSender.SentMessages);
    }

    [Fact]
    public async Task DispatchLowStock_OnlyDispatchesWhenBalanceDropsBelowPositiveMinimum()
    {
        var notifications = new FakeNotificationRepository();
        var recipients = new FakeRecipientRepository([]);
        var emailSender = new FakeSender(NotificationChannel.Email);
        var whatsAppSender = new FakeSender(NotificationChannel.WhatsApp);
        var dispatcher = new NotificationDispatcher(notifications, recipients, [emailSender, whatsAppSender]);

        var now = DateTime.UtcNow;
        var sufficientStock = new StockMovementRecordedEvent(
            Guid.NewGuid(), now, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Issue", 3, 10, 7, now, "DEMO-HER-001", "Demo hammer", 5, null);

        var notTriggered = await dispatcher.DispatchLowStockIfTriggeredAsync(sufficientStock, CancellationToken.None);
        Assert.Empty(notTriggered);
        Assert.Empty(notifications.Items);

        var movementId = Guid.NewGuid();
        var lowStock = sufficientStock with
        {
            EventId = movementId,
            MovementId = movementId,
            PreviousQuantity = 7,
            NewQuantity = 4,
            MinimumStock = 5
        };

        var triggered = await dispatcher.DispatchLowStockIfTriggeredAsync(lowStock, CancellationToken.None);
        Assert.Equal(2, triggered.Count);
        Assert.Contains(triggered, n => n.Channel == NotificationChannel.Email && n.Status == NotificationStatus.Sent);
        Assert.Contains(triggered, n => n.Channel == NotificationChannel.WhatsApp && n.Status == NotificationStatus.Sent);
    }

    [Fact]
    public async Task Dispatch_WhenSenderFails_RecordsFailedNotificationWithoutThrowing()
    {
        var notifications = new FakeNotificationRepository();
        var recipients = new FakeRecipientRepository(
        [
            NotificationRecipient.Create("Owner Email", NotificationChannel.Email, "owner@stocksync.demo")
        ]);
        var failingSender = new FakeSender(NotificationChannel.Email, shouldFail: true);
        var dispatcher = new NotificationDispatcher(notifications, recipients, [failingSender]);

        var orderEvent = new OrderCreatedEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var created = await dispatcher.DispatchOrderCreatedAsync(orderEvent, CancellationToken.None);

        var notification = Assert.Single(created);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Contains("SMTP gateway timeout", notification.FailureReason);
    }

    private sealed class FakeSender(NotificationChannel channel, bool shouldFail = false) : INotificationChannelSender
    {
        public NotificationChannel Channel => channel;
        public List<(string Recipient, string Subject, string Body)> SentMessages { get; } = [];

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
        {
            if (shouldFail)
                throw new InvalidOperationException("SMTP gateway timeout.");

            SentMessages.Add((recipient, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<Notification.Domain.Entities.Notification> Items { get; } = [];

        public Task<Notification.Domain.Entities.Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(n => n.Id == id));

        public Task<(IReadOnlyList<Notification.Domain.Entities.Notification> Items, int Total)> ListAsync(
            NotificationChannel? channel, NotificationEventType? eventType, NotificationStatus? status,
            Guid? branchId, DateTime? from, DateTime? to, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<Notification.Domain.Entities.Notification>)Items, Items.Count));

        public Task AddAsync(Notification.Domain.Entities.Notification notification, CancellationToken cancellationToken)
        {
            Items.Add(notification);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRecipientRepository(List<NotificationRecipient> items) : INotificationRecipientRepository
    {
        public Task<NotificationRecipient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(items.FirstOrDefault(r => r.Id == id && r.IsActive));

        public Task<NotificationRecipient?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            GetByIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<NotificationRecipient> Items, int Total)> ListAsync(
            NotificationChannel? channel, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<NotificationRecipient>)items, items.Count));

        public Task<IReadOnlyList<NotificationRecipient>> ListActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult((IReadOnlyList<NotificationRecipient>)items.Where(r => r.IsActive).ToList());

        public Task<bool> DestinationExistsAsync(
            NotificationChannel channel, string normalizedDestination, Guid? excludeId, CancellationToken cancellationToken) =>
            Task.FromResult(items.Any(r =>
                r.IsActive && r.Channel == channel && r.NormalizedDestination == normalizedDestination && r.Id != excludeId));

        public Task AddAsync(NotificationRecipient recipient, CancellationToken cancellationToken)
        {
            items.Add(recipient);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
