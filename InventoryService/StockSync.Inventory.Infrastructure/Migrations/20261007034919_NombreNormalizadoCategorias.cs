using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockSync.Inventory.Infrastructure.Migrations
{
    public partial class NombreNormalizadoCategorias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categorias_Nombre",
                table: "Categorias");

            migrationBuilder.AddColumn<string>(
                name: "NombreNormalizado",
                table: "Categorias",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Las categorías existentes deben quedar normalizadas antes de crear el índice único.
            migrationBuilder.Sql("UPDATE \"Categorias\" SET \"NombreNormalizado\" = UPPER(TRIM(\"Nombre\"));");
            migrationBuilder.Sql("ALTER TABLE \"Categorias\" ALTER COLUMN \"NombreNormalizado\" DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_NombreNormalizado",
                table: "Categorias",
                column: "NombreNormalizado",
                unique: true,
                filter: "\"Activo\"");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categorias_NombreNormalizado",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "NombreNormalizado",
                table: "Categorias");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Nombre",
                table: "Categorias",
                column: "Nombre",
                unique: true,
                filter: "\"Activo\"");
        }
    }
}
