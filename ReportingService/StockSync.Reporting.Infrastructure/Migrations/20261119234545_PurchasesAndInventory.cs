using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockSync.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchasesAndInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Total = table.Column<decimal>(type: "numeric", nullable: false),
                    Units = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchases", x => new { x.TenantId, x.PurchaseOrderId });
                });

            migrationBuilder.CreateTable(
                name: "stock_levels",
                columns: table => new
                {
                    StockId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    MinimumStock = table.Column<int>(type: "integer", nullable: false),
                    LastOccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMovementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_levels", x => new { x.TenantId, x.StockId });
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    MovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PreviousQuantity = table.Column<int>(type: "integer", nullable: false),
                    NewQuantity = table.Column<int>(type: "integer", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    MinimumStock = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.MovementId);
                });

            migrationBuilder.CreateTable(
                name: "purchase_lines",
                columns: table => new
                {
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_lines", x => new { x.TenantId, x.PurchaseOrderId, x.LineNumber });
                    table.ForeignKey(
                        name: "FK_purchase_lines_purchases_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalTable: "purchases",
                        principalColumns: new[] { "TenantId", "PurchaseOrderId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_TenantId_BranchId_ReceivedAt",
                table: "purchases",
                columns: new[] { "TenantId", "BranchId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_TenantId_ReceivedAt",
                table: "purchases",
                columns: new[] { "TenantId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_levels_TenantId_BranchId",
                table: "stock_levels",
                columns: new[] { "TenantId", "BranchId" },
                filter: "\"MinimumStock\" > 0 AND \"Quantity\" < \"MinimumStock\"");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_BranchId_MovementDate",
                table: "stock_movements",
                columns: new[] { "TenantId", "BranchId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_MovementDate",
                table: "stock_movements",
                columns: new[] { "TenantId", "MovementDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_lines");

            migrationBuilder.DropTable(
                name: "stock_levels");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "purchases");
        }
    }
}
