using Microsoft.EntityFrameworkCore;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.Infrastructure;

public sealed class StockMovementProjection(ReportingDbContext db, TenantContext tenant)
    : EventProjection<StockMovementRecorded>(db, tenant)
{
    protected override async Task ProjectAsync(StockMovementRecorded message, CancellationToken token)
    {
        var row = message.ProjectMovement();
        SetTenant(row);
        Db.StockMovements.Add(row);

        var level = message.ProjectLevel();
        await Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO stock_levels AS current
                ("TenantId", "StockId", "BranchId", "ProductId", "ProductSku", "ProductName",
                 "Quantity", "MinimumStock", "LastOccurredAt", "LastMovementId")
            VALUES ({message.TenantId}, {level.StockId}, {level.BranchId}, {level.ProductId}, {level.ProductSku},
                    {level.ProductName}, {level.Quantity}, {level.MinimumStock}, {level.LastOccurredAt}, {level.LastMovementId})
            ON CONFLICT ("TenantId", "StockId") DO UPDATE SET
                "BranchId" = EXCLUDED."BranchId", "ProductId" = EXCLUDED."ProductId",
                "ProductSku" = EXCLUDED."ProductSku", "ProductName" = EXCLUDED."ProductName",
                "Quantity" = EXCLUDED."Quantity", "MinimumStock" = EXCLUDED."MinimumStock",
                "LastOccurredAt" = EXCLUDED."LastOccurredAt", "LastMovementId" = EXCLUDED."LastMovementId"
            WHERE (EXCLUDED."LastOccurredAt", EXCLUDED."LastMovementId") > (current."LastOccurredAt", current."LastMovementId")
            """, token);
    }
}
