using StockSync.Notification.Domain.Entities;
using StockSync.Notification.Domain.Exceptions;

namespace StockSync.Notification.UnitTests.Domain;

public class NotificationTests
{
    [Fact]
    public void Create_WithValidArguments_InitializesPendingNotification()
    {
        var eventId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var notification = Notification.Domain.Entities.Notification.Create(
            eventId,
            NotificationEventType.LowStock,
            NotificationChannel.Email,
            " owner@stocksync.demo ",
            " Low stock alert ",
            " Product is below minimum. ",
            branchId,
            resourceId);

        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(eventId, notification.SourceEventId);
        Assert.Equal(NotificationEventType.LowStock, notification.EventType);
        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal("owner@stocksync.demo", notification.Recipient);
        Assert.Equal("Low stock alert", notification.Subject);
        Assert.Equal("Product is below minimum.", notification.Body);
        Assert.Equal(branchId, notification.BranchId);
        Assert.Equal(resourceId, notification.ResourceId);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Null(notification.SentAt);
        Assert.Null(notification.FailureReason);
    }

    [Fact]
    public void MarkSent_UpdatesStatusAndSentTimestamp()
    {
        var notification = Notification.Domain.Entities.Notification.Create(
            Guid.NewGuid(),
            NotificationEventType.OrderCreated,
            NotificationChannel.WhatsApp,
            "+15550100100",
            "Order created",
            "New order created.",
            Guid.NewGuid(),
            Guid.NewGuid());

        notification.MarkSent();

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.NotNull(notification.SentAt);
        Assert.Null(notification.FailureReason);
        Assert.Throws<DomainException>(() => notification.MarkSent());
    }

    [Fact]
    public void MarkFailed_StoresFailureReasonAndClearsSentAt()
    {
        var notification = Notification.Domain.Entities.Notification.Create(
            Guid.NewGuid(),
            NotificationEventType.OrderCreated,
            NotificationChannel.Email,
            "owner@stocksync.demo",
            "Order created",
            "New order created.",
            Guid.NewGuid(),
            Guid.NewGuid());

        notification.MarkFailed("SMTP connection refused.");

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Null(notification.SentAt);
        Assert.Equal("SMTP connection refused.", notification.FailureReason);
    }
}
