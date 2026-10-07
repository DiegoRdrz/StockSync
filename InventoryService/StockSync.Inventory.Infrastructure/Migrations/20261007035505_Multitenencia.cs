using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockSync.Inventory.Infrastructure.Migrations
{
    public partial class Multitenencia : Migration
    {
        private static readonly string[] Tablas = ["Categorias", "Productos", "Stocks", "MovimientosStock"];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stocks_SucursalId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Productos_Sku",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_NombreNormalizado",
                table: "Categorias");

            // Los datos existentes pasan al tenant por defecto; después se quita el DEFAULT para que
            // ninguna fila nueva quede asignada a un tenant sin que la aplicación lo indique.
            foreach (var tabla in Tablas)
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "TenantId",
                    table: tabla,
                    type: "uuid",
                    nullable: false,
                    defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

                migrationBuilder.Sql($"ALTER TABLE \"{tabla}\" ALTER COLUMN \"TenantId\" DROP DEFAULT;");
            }

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_TenantId_SucursalId",
                table: "Stocks",
                columns: new[] { "TenantId", "SucursalId" });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_TenantId_Sku",
                table: "Productos",
                columns: new[] { "TenantId", "Sku" },
                unique: true,
                filter: "\"Activo\"");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_TenantId_Fecha_Id",
                table: "MovimientosStock",
                columns: new[] { "TenantId", "Fecha", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_TenantId_NombreNormalizado",
                table: "Categorias",
                columns: new[] { "TenantId", "NombreNormalizado" },
                unique: true,
                filter: "\"Activo\"");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stocks_TenantId_SucursalId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Productos_TenantId_Sku",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosStock_TenantId_Fecha_Id",
                table: "MovimientosStock");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_TenantId_NombreNormalizado",
                table: "Categorias");

            foreach (var tabla in Tablas)
            {
                migrationBuilder.DropColumn(
                    name: "TenantId",
                    table: tabla);
            }

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_SucursalId",
                table: "Stocks",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Sku",
                table: "Productos",
                column: "Sku",
                unique: true,
                filter: "\"Activo\"");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_NombreNormalizado",
                table: "Categorias",
                column: "NombreNormalizado",
                unique: true,
                filter: "\"Activo\"");
        }
    }
}
