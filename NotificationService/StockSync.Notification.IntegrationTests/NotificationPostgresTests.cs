using Microsoft.Extensions.Logging.Abstractions;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.IntegrationEvents;
using StockSync.Notification.Application.Recipients;
using StockSync.Notification.Domain.Entities;
using StockSync.Notification.Infrastructure;
using StockSync.Notification.Infrastructure.Messaging;
using StockSync.Notification.Infrastructure.Repositories;
using StockSync.Notification.Infrastructure.Senders;

namespace StockSync.Notification.IntegrationTests;

public class NotificationPostgresTests : PostgresTestBase
{
    [PostgresFact]
    public async Task OrderCreatedProcessor_DeduplicatesByEventIdAndPersistsTenantNotifications()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.Set(tenantId);

        await using var context = CreateContext(tenant);
        await NotificationDbSeeder.SeedAsync(context);

        var notificationRepo = new NotificationRepository(context);
        var recipientRepo = new NotificationRecipientRepository(context);
        var dispatcher = new NotificationDispatcher(
            notificationRepo,
            recipientRepo,
            [
                new EmailNotificationSender(new SmtpOptions(null), NullLogger<EmailNotificationSender>.Instance),
                new WhatsAppNotificationSender(new HttpClient(), new WhatsAppOptions(null), NullLogger<WhatsAppNotificationSender>.Instance)
            ]);
        var processor = new OrderCreatedEventProcessor(context, tenant, dispatcher);

        var orderEvent = new OrderCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        var first = await processor.ProcessAsync(orderEvent, CancellationToken.None);
        var second = await processor.ProcessAsync(orderEvent, CancellationToken.None);

        Assert.Equal(EventProcessingOutcome.Applied, first);
        Assert.Equal(EventProcessingOutcome.Duplicate, second);

        var (items, total) = await notificationRepo.ListAsync(
            null, null, null, null, null, null, 0, 20, CancellationToken.None);
        Assert.Equal(2, total);
        Assert.Equal(2, items.Count);

        await using var otherTenantContext = CreateContext(Guid.NewGuid());
        var otherRepo = new NotificationRepository(otherTenantContext);
        var (_, otherTotal) = await otherRepo.ListAsync(
            null, null, null, null, null, null, 0, 20, CancellationToken.None);
        Assert.Equal(0, otherTotal);
    }

    [PostgresFact]
    public async Task DeactivatedRecipientDestination_CanBeReusedWithinSameTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var service = new NotificationRecipientService(new NotificationRecipientRepository(context));

        var created = await service.CreateAsync(
            new NotificationRecipientRequest("Primary Email", NotificationChannel.Email, "alerts@stocksync.demo"),
            CancellationToken.None);

        await service.DeleteAsync(created.Id, CancellationToken.None);

        var recreated = await service.CreateAsync(
            new NotificationRecipientRequest("Replacement Email", NotificationChannel.Email, "alerts@stocksync.demo"),
            CancellationToken.None);

        Assert.NotEqual(created.Id, recreated.Id);
        Assert.True(recreated.IsActive);
    }
}
