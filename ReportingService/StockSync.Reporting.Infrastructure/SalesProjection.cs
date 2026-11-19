using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.Infrastructure;

public sealed class SalesProjection(ReportingDbContext db, TenantContext tenant) : ISalesProjection
{
    public const string ConsumerName = "reporting.sales";

    public async Task<ProjectionResult> ApplyAsync(SaleConfirmed sale, CancellationToken token)
    {
        tenant.Set(sale.TenantId);
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        db.ProcessedEvents.Add(new(ConsumerName, sale.EventId, DateTime.UtcNow));
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_processed_events" })
        {
            await transaction.RollbackAsync(token);
            return ProjectionResult.Duplicate;
        }

        var row = sale.Project();
        db.Entry(row).Property("TenantId").CurrentValue = sale.TenantId;
        foreach (var line in row.Lines)
            db.Entry(line).Property("TenantId").CurrentValue = sale.TenantId;
        db.Sales.Add(row);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return ProjectionResult.Applied;
    }
}
