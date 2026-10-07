using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockSync.Inventory.Infrastructure.Migrations
{
    public partial class AjusteEnMovimientosStock : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientosStock_Tipo",
                table: "MovimientosStock");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientosStock_Tipo",
                table: "MovimientosStock",
                sql: "\"Tipo\" IN ('Entrada', 'Salida', 'Ajuste')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientosStock_Tipo",
                table: "MovimientosStock");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientosStock_Tipo",
                table: "MovimientosStock",
                sql: "\"Tipo\" IN ('Entrada', 'Salida')");
        }
    }
}
