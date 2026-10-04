namespace StockSync.Inventory.Application.Productos;

public sealed record ProductoRequest(
    string Nombre,
    string Sku,
    string? Descripcion,
    decimal PrecioCompra,
    decimal PrecioVenta,
    int StockMinimo,
    Guid? CategoriaId);
