using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockSync.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMovimientosStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovimientosStock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    CantidadAnterior = table.Column<int>(type: "integer", nullable: false),
                    CantidadPosterior = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosStock", x => x.Id);
                    table.CheckConstraint("CK_MovimientosStock_Cantidad", "\"Cantidad\" > 0");
                    table.CheckConstraint("CK_MovimientosStock_Saldos", "\"CantidadAnterior\" >= 0 AND \"CantidadPosterior\" >= 0");
                    table.CheckConstraint("CK_MovimientosStock_Tipo", "\"Tipo\" IN ('Entrada', 'Salida')");
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_Cantidad",
                table: "Stocks",
                sql: "\"Cantidad\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_StockId_Fecha_Id",
                table: "MovimientosStock",
                columns: new[] { "StockId", "Fecha", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosStock");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_Cantidad",
                table: "Stocks");
        }
    }
}
