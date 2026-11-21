using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StockSync.Reporting.Infrastructure.Migrations;

[DbContext(typeof(ReportingDbContext))]
[Migration("20261119000000_InitialReporting")]
public sealed class InitialReporting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "processed_events",
            columns: table => new
            {
                ConsumerName = table.Column<string>(nullable: false),
                EventId = table.Column<Guid>(nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_processed_events", x => new { x.ConsumerName, x.EventId }));
        migrationBuilder.CreateTable(
            name: "sales",
            columns: table => new
            {
                TenantId = table.Column<Guid>(nullable: false),
                OrderId = table.Column<Guid>(nullable: false),
                BranchId = table.Column<Guid>(nullable: false),
                ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Total = table.Column<decimal>(nullable: false),
                Units = table.Column<long>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_sales", x => new { x.TenantId, x.OrderId }));
        migrationBuilder.CreateTable(
            name: "sale_lines",
            columns: table => new
            {
                TenantId = table.Column<Guid>(nullable: false),
                OrderId = table.Column<Guid>(nullable: false),
                LineNumber = table.Column<int>(nullable: false),
                ProductId = table.Column<Guid>(nullable: false),
                ProductSku = table.Column<string>(nullable: false),
                ProductName = table.Column<string>(nullable: false),
                Quantity = table.Column<int>(nullable: false),
                UnitPrice = table.Column<decimal>(nullable: false),
                LineTotal = table.Column<decimal>(nullable: false),
                StockMovementId = table.Column<Guid>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sale_lines", x => new { x.TenantId, x.OrderId, x.LineNumber });
                table.ForeignKey("FK_sale_lines_sales_TenantId_OrderId", x => new { x.TenantId, x.OrderId },
                    "sales", new[] { "TenantId", "OrderId" }, onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_sales_TenantId_ConfirmedAt_OrderId", "sales", new[] { "TenantId", "ConfirmedAt", "OrderId" });
        migrationBuilder.CreateIndex("IX_sales_TenantId_BranchId_ConfirmedAt", "sales", new[] { "TenantId", "BranchId", "ConfirmedAt" });
        migrationBuilder.CreateIndex("IX_sale_lines_TenantId_ProductId", "sale_lines", new[] { "TenantId", "ProductId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("sale_lines");
        migrationBuilder.DropTable("sales");
        migrationBuilder.DropTable("processed_events");
    }
}
