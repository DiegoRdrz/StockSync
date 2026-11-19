using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.Infrastructure;

public abstract class EventProjection<T>(ReportingDbContext db, TenantContext tenant) : IProjection<T>
    where T : IReportingEvent<T>
{
    protected ReportingDbContext Db { get; } = db;

    protected abstract Task ProjectAsync(T message, CancellationToken token);

    public async Task<ProjectionResult> ApplyAsync(T message, CancellationToken token)
    {
        tenant.Set(message.TenantId);
        Db.ChangeTracker.Clear();
        await using var transaction = await Db.Database.BeginTransactionAsync(token);
        Db.ProcessedEvents.Add(new(T.ConsumerName, message.EventId, DateTime.UtcNow));
        try
        {
            await Db.SaveChangesAsync(token);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_processed_events" })
        {
            await transaction.RollbackAsync(token);
            return ProjectionResult.Duplicate;
        }

        await ProjectAsync(message, token);
        await Db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return ProjectionResult.Applied;
    }

    protected void SetTenant<TEntity>(TEntity row) where TEntity : class =>
        Db.Entry(row).Property("TenantId").CurrentValue = tenant.TenantId;
}
