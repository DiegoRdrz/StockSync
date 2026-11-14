using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockSync.Inventory.Infrastructure.Migrations;

// Rename existing objects in place to preserve IDs, balances, tenant ownership and history.
// Legacy names are required here to upgrade and roll back already-deployed databases.
public partial class TranslateSchemaToEnglish : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Categorias" RENAME TO "Categories";
            ALTER TABLE "Productos" RENAME TO "Products";
            ALTER TABLE "MovimientosStock" RENAME TO "StockMovements";
            ALTER TABLE "Categories" RENAME COLUMN "Nombre" TO "Name";
            ALTER TABLE "Categories" RENAME COLUMN "Descripcion" TO "Description";
            ALTER TABLE "Categories" RENAME COLUMN "Activo" TO "IsActive";
            ALTER TABLE "Categories" RENAME COLUMN "FechaCreacion" TO "CreatedAt";
            ALTER TABLE "Categories" RENAME COLUMN "FechaActualizacion" TO "UpdatedAt";
            ALTER TABLE "Categories" RENAME COLUMN "NombreNormalizado" TO "NormalizedName";
            ALTER TABLE "Products" RENAME COLUMN "Nombre" TO "Name";
            ALTER TABLE "Products" RENAME COLUMN "Descripcion" TO "Description";
            ALTER TABLE "Products" RENAME COLUMN "Activo" TO "IsActive";
            ALTER TABLE "Products" RENAME COLUMN "FechaCreacion" TO "CreatedAt";
            ALTER TABLE "Products" RENAME COLUMN "FechaActualizacion" TO "UpdatedAt";
            ALTER TABLE "Products" RENAME COLUMN "PrecioCompra" TO "PurchasePrice";
            ALTER TABLE "Products" RENAME COLUMN "PrecioVenta" TO "SalePrice";
            ALTER TABLE "Products" RENAME COLUMN "StockMinimo" TO "MinimumStock";
            ALTER TABLE "Products" RENAME COLUMN "CategoriaId" TO "CategoryId";
            ALTER TABLE "StockMovements" RENAME COLUMN "Tipo" TO "Type";
            ALTER TABLE "StockMovements" RENAME COLUMN "Cantidad" TO "Quantity";
            ALTER TABLE "StockMovements" RENAME COLUMN "CantidadAnterior" TO "PreviousQuantity";
            ALTER TABLE "StockMovements" RENAME COLUMN "CantidadPosterior" TO "NewQuantity";
            ALTER TABLE "StockMovements" RENAME COLUMN "Fecha" TO "Date";
            ALTER TABLE "Stocks" RENAME COLUMN "ProductoId" TO "ProductId";
            ALTER TABLE "Stocks" RENAME COLUMN "SucursalId" TO "BranchId";
            ALTER TABLE "Stocks" RENAME COLUMN "Cantidad" TO "Quantity";
            ALTER TABLE "Categories" RENAME CONSTRAINT "PK_Categorias" TO "PK_Categories";
            ALTER TABLE "Products" RENAME CONSTRAINT "PK_Productos" TO "PK_Products";
            ALTER TABLE "Products" RENAME CONSTRAINT "FK_Productos_Categorias_CategoriaId" TO "FK_Products_Categories_CategoryId";
            ALTER TABLE "StockMovements" RENAME CONSTRAINT "PK_MovimientosStock" TO "PK_StockMovements";
            ALTER TABLE "StockMovements" RENAME CONSTRAINT "FK_MovimientosStock_Stocks_StockId" TO "FK_StockMovements_Stocks_StockId";
            ALTER TABLE "StockMovements" RENAME CONSTRAINT "CK_MovimientosStock_Cantidad" TO "CK_StockMovements_Quantity";
            ALTER TABLE "StockMovements" RENAME CONSTRAINT "CK_MovimientosStock_Saldos" TO "CK_StockMovements_Balances";
            ALTER TABLE "Stocks" RENAME CONSTRAINT "FK_Stocks_Productos_ProductoId" TO "FK_Stocks_Products_ProductId";
            ALTER TABLE "Stocks" RENAME CONSTRAINT "CK_Stocks_Cantidad" TO "CK_Stocks_Quantity";
            ALTER INDEX "IX_Categorias_TenantId_NombreNormalizado" RENAME TO "IX_Categories_TenantId_NormalizedName";
            ALTER INDEX "IX_Productos_CategoriaId" RENAME TO "IX_Products_CategoryId";
            ALTER INDEX "IX_Productos_TenantId_Sku" RENAME TO "IX_Products_TenantId_Sku";
            ALTER INDEX "IX_MovimientosStock_StockId_Fecha_Id" RENAME TO "IX_StockMovements_StockId_Date_Id";
            ALTER INDEX "IX_MovimientosStock_TenantId_Fecha_Id" RENAME TO "IX_StockMovements_TenantId_Date_Id";
            ALTER INDEX "IX_Stocks_ProductoId_SucursalId" RENAME TO "IX_Stocks_ProductId_BranchId";
            ALTER INDEX "IX_Stocks_TenantId_SucursalId" RENAME TO "IX_Stocks_TenantId_BranchId";
            ALTER TABLE "StockMovements" DROP CONSTRAINT "CK_MovimientosStock_Tipo";
            UPDATE "StockMovements" SET "Type" = CASE "Type"
                WHEN 'Entrada' THEN 'Receipt' WHEN 'Salida' THEN 'Issue' WHEN 'Ajuste' THEN 'Adjustment' END;
            ALTER TABLE "StockMovements" ADD CONSTRAINT "CK_StockMovements_Type"
                CHECK ("Type" IN ('Receipt', 'Issue', 'Adjustment'));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "StockMovements" DROP CONSTRAINT "CK_StockMovements_Type";
            UPDATE "StockMovements" SET "Type" = CASE "Type"
                WHEN 'Receipt' THEN 'Entrada' WHEN 'Issue' THEN 'Salida' WHEN 'Adjustment' THEN 'Ajuste' END;
            ALTER TABLE "StockMovements" ADD CONSTRAINT "CK_MovimientosStock_Tipo"
                CHECK ("Type" IN ('Entrada', 'Salida', 'Ajuste'));
            ALTER INDEX "IX_Stocks_TenantId_BranchId" RENAME TO "IX_Stocks_TenantId_SucursalId";
            ALTER INDEX "IX_Stocks_ProductId_BranchId" RENAME TO "IX_Stocks_ProductoId_SucursalId";
            ALTER INDEX "IX_StockMovements_TenantId_Date_Id" RENAME TO "IX_MovimientosStock_TenantId_Fecha_Id";
            ALTER INDEX "IX_StockMovements_StockId_Date_Id" RENAME TO "IX_MovimientosStock_StockId_Fecha_Id";
            ALTER INDEX "IX_Products_TenantId_Sku" RENAME TO "IX_Productos_TenantId_Sku";
            ALTER INDEX "IX_Products_CategoryId" RENAME TO "IX_Productos_CategoriaId";
            ALTER INDEX "IX_Categories_TenantId_NormalizedName" RENAME TO "IX_Categorias_TenantId_NombreNormalizado";
            ALTER TABLE "Stocks" RENAME CONSTRAINT "CK_Stocks_Quantity" TO "CK_Stocks_Cantidad";
            ALTER TABLE "Stocks" RENAME CONSTRAINT "FK_Stocks_Products_ProductId" TO "FK_Stocks_Productos_ProductoId";
            ALTER TABLE "StockMovements" RENAME CONSTRAINT "CK_StockMovements_Balances" TO "CK_MovimientosStock_Saldos";
            ALTER TABLE "StockMovements" RENAME CONSTRAINT "CK_StockMovements_Quantity" TO "CK_MovimientosStock_Cantidad";
            ALTER TABLE "StockMovements" RENAME CONSTRAINT "FK_StockMovements_Stocks_StockId" TO "FK_MovimientosStock_Stocks_StockId";
            ALTER TABLE "StockMovements" RENAME CONSTRAINT "PK_StockMovements" TO "PK_MovimientosStock";
            ALTER TABLE "Products" RENAME CONSTRAINT "FK_Products_Categories_CategoryId" TO "FK_Productos_Categorias_CategoriaId";
            ALTER TABLE "Products" RENAME CONSTRAINT "PK_Products" TO "PK_Productos";
            ALTER TABLE "Categories" RENAME CONSTRAINT "PK_Categories" TO "PK_Categorias";
            ALTER TABLE "Stocks" RENAME COLUMN "Quantity" TO "Cantidad";
            ALTER TABLE "Stocks" RENAME COLUMN "BranchId" TO "SucursalId";
            ALTER TABLE "Stocks" RENAME COLUMN "ProductId" TO "ProductoId";
            ALTER TABLE "StockMovements" RENAME COLUMN "Date" TO "Fecha";
            ALTER TABLE "StockMovements" RENAME COLUMN "NewQuantity" TO "CantidadPosterior";
            ALTER TABLE "StockMovements" RENAME COLUMN "PreviousQuantity" TO "CantidadAnterior";
            ALTER TABLE "StockMovements" RENAME COLUMN "Quantity" TO "Cantidad";
            ALTER TABLE "StockMovements" RENAME COLUMN "Type" TO "Tipo";
            ALTER TABLE "Products" RENAME COLUMN "CategoryId" TO "CategoriaId";
            ALTER TABLE "Products" RENAME COLUMN "MinimumStock" TO "StockMinimo";
            ALTER TABLE "Products" RENAME COLUMN "SalePrice" TO "PrecioVenta";
            ALTER TABLE "Products" RENAME COLUMN "PurchasePrice" TO "PrecioCompra";
            ALTER TABLE "Products" RENAME COLUMN "UpdatedAt" TO "FechaActualizacion";
            ALTER TABLE "Products" RENAME COLUMN "CreatedAt" TO "FechaCreacion";
            ALTER TABLE "Products" RENAME COLUMN "IsActive" TO "Activo";
            ALTER TABLE "Products" RENAME COLUMN "Description" TO "Descripcion";
            ALTER TABLE "Products" RENAME COLUMN "Name" TO "Nombre";
            ALTER TABLE "Categories" RENAME COLUMN "NormalizedName" TO "NombreNormalizado";
            ALTER TABLE "Categories" RENAME COLUMN "UpdatedAt" TO "FechaActualizacion";
            ALTER TABLE "Categories" RENAME COLUMN "CreatedAt" TO "FechaCreacion";
            ALTER TABLE "Categories" RENAME COLUMN "IsActive" TO "Activo";
            ALTER TABLE "Categories" RENAME COLUMN "Description" TO "Descripcion";
            ALTER TABLE "Categories" RENAME COLUMN "Name" TO "Nombre";
            ALTER TABLE "StockMovements" RENAME TO "MovimientosStock";
            ALTER TABLE "Products" RENAME TO "Productos";
            ALTER TABLE "Categories" RENAME TO "Categorias";
            """);
    }

}
