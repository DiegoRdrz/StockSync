using Microsoft.EntityFrameworkCore;
using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Infrastructure;

public static class NotificationDbSeeder
{
    public static async Task SeedAsync(NotificationDbContext context, CancellationToken cancellationToken = default)
    {
        await EnsureRecipientAsync("Demo Store Owner (Email)", NotificationChannel.Email, "owner@stocksync.demo");
        await EnsureRecipientAsync("Demo Store Owner (WhatsApp)", NotificationChannel.WhatsApp, "+15550100100");

        await context.SaveChangesAsync(cancellationToken);

        async Task EnsureRecipientAsync(string name, NotificationChannel channel, string destination)
        {
            var normalized = NotificationRecipient.NormalizeDestination(channel, destination);
            var exists = await context.NotificationRecipients.AnyAsync(
                r => r.IsActive && r.Channel == channel && r.NormalizedDestination == normalized,
                cancellationToken);
            if (exists)
                return;

            context.NotificationRecipients.Add(NotificationRecipient.Create(name, channel, destination));
        }
    }
}
