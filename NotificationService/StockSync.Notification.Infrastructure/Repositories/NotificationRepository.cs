using Microsoft.EntityFrameworkCore;
using StockSync.Notification.Domain.Entities;
using StockSync.Notification.Domain.Repositories;

namespace StockSync.Notification.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _context;

    public NotificationRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public Task<Domain.Entities.Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Domain.Entities.Notification> Items, int Total)> ListAsync(
        NotificationChannel? channel,
        NotificationEventType? eventType,
        NotificationStatus? status,
        Guid? branchId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.Notifications.AsNoTracking();

        if (channel.HasValue)
            query = query.Where(n => n.Channel == channel.Value);

        if (eventType.HasValue)
            query = query.Where(n => n.EventType == eventType.Value);

        if (status.HasValue)
            query = query.Where(n => n.Status == status.Value);

        if (branchId.HasValue)
            query = query.Where(n => n.BranchId == branchId.Value);

        if (from.HasValue)
            query = query.Where(n => n.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(n => n.CreatedAt <= to.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(Domain.Entities.Notification notification, CancellationToken cancellationToken) =>
        await _context.Notifications.AddAsync(notification, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
