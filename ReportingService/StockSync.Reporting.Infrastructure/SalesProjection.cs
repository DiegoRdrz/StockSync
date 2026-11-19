using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.Infrastructure;

public sealed class SalesProjection(ReportingDbContext db, TenantContext tenant)
    : EventProjection<SaleConfirmed>(db, tenant)
{
    protected override Task ProjectAsync(SaleConfirmed sale, CancellationToken token)
    {
        var row = sale.Project();
        SetTenant(row);
        foreach (var line in row.Lines) SetTenant(line);
        Db.Sales.Add(row);
        return Task.CompletedTask;
    }
}
