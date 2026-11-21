using StockSync.Notification.Domain.Entities;
using StockSync.Notification.Domain.Exceptions;

namespace StockSync.Notification.UnitTests.Domain;

public class NotificationRecipientTests
{
    [Fact]
    public void Create_EmailRecipient_TrimsAndNormalizesUppercase()
    {
        var recipient = NotificationRecipient.Create(
            " Store Owner ",
            NotificationChannel.Email,
            " Owner@StockSync.Demo ");

        Assert.Equal("Store Owner", recipient.Name);
        Assert.Equal(NotificationChannel.Email, recipient.Channel);
        Assert.Equal("Owner@StockSync.Demo", recipient.Destination);
        Assert.Equal("OWNER@STOCKSYNC.DEMO", recipient.NormalizedDestination);
        Assert.True(recipient.IsActive);
    }

    [Fact]
    public void Create_WhatsAppRecipient_NormalizesFormatting()
    {
        var recipient = NotificationRecipient.Create(
            "Warehouse Lead",
            NotificationChannel.WhatsApp,
            " +57 (300) 123-4567 ");

        Assert.Equal("+573001234567", recipient.Destination);
        Assert.Equal("+573001234567", recipient.NormalizedDestination);
    }

    [Theory]
    [InlineData(NotificationChannel.Email, "invalid-email")]
    [InlineData(NotificationChannel.Email, "user@localhost")]
    [InlineData(NotificationChannel.WhatsApp, "12345")]
    [InlineData(NotificationChannel.WhatsApp, "abc12345678")]
    public void Create_WithInvalidDestination_ThrowsDomainException(
        NotificationChannel channel,
        string destination)
    {
        Assert.Throws<DomainException>(() =>
            NotificationRecipient.Create("Recipient", channel, destination));
    }

    [Fact]
    public void Deactivate_MarksInactiveAndBlocksFurtherUpdates()
    {
        var recipient = NotificationRecipient.Create(
            "Store Owner",
            NotificationChannel.Email,
            "owner@stocksync.demo");

        recipient.Deactivate();

        Assert.False(recipient.IsActive);
        Assert.NotNull(recipient.UpdatedAt);
        Assert.Throws<DomainException>(() =>
            recipient.Update("Updated Name", NotificationChannel.Email, "new@stocksync.demo"));
        Assert.Throws<DomainException>(() => recipient.Deactivate());
    }
}
