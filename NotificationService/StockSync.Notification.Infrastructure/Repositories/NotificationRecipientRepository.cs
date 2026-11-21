using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Notification.Application.Common.Exceptions;
using StockSync.Notification.Domain.Entities;
using StockSync.Notification.Domain.Repositories;

namespace StockSync.Notification.Infrastructure.Repositories;

public class NotificationRecipientRepository : INotificationRecipientRepository
{
    private readonly NotificationDbContext _context;

    public NotificationRecipientRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public Task<NotificationRecipient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.NotificationRecipients
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, cancellationToken);

    public Task<NotificationRecipient?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        _context.NotificationRecipients
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, cancellationToken);

    public async Task<(IReadOnlyList<NotificationRecipient> Items, int Total)> ListAsync(
        NotificationChannel? channel,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.NotificationRecipients.AsNoTracking().Where(r => r.IsActive);

        if (channel.HasValue)
            query = query.Where(r => r.Channel == channel.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.Channel)
            .ThenBy(r => r.Name)
            .ThenBy(r => r.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ListActiveAsync(CancellationToken cancellationToken) =>
        await _context.NotificationRecipients
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Channel)
            .ThenBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> DestinationExistsAsync(
        NotificationChannel channel,
        string normalizedDestination,
        Guid? excludeId,
        CancellationToken cancellationToken) =>
        _context.NotificationRecipients.AnyAsync(
            r => r.IsActive
                 && r.Channel == channel
                 && r.NormalizedDestination == normalizedDestination
                 && (excludeId == null || r.Id != excludeId),
            cancellationToken);

    public async Task AddAsync(NotificationRecipient recipient, CancellationToken cancellationToken) =>
        await _context.NotificationRecipients.AddAsync(recipient, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("An active recipient with the same channel and destination already exists.");
        }
    }
}
