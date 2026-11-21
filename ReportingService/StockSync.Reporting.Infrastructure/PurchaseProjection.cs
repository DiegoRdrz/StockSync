using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.Infrastructure;

public sealed class PurchaseProjection(ReportingDbContext db, TenantContext tenant)
    : EventProjection<PurchaseOrderReceived>(db, tenant)
{
    protected override Task ProjectAsync(PurchaseOrderReceived purchase, CancellationToken token)
    {
        var row = purchase.Project();
        SetTenant(row);
        foreach (var line in row.Lines) SetTenant(line);
        Db.Purchases.Add(row);
        return Task.CompletedTask;
    }
}
