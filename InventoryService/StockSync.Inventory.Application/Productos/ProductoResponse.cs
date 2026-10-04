using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Productos;

public sealed record ProductoResponse(
    Guid Id,
    string Nombre,
    string Sku,
    string? Descripcion,
    decimal PrecioCompra,
    decimal PrecioVenta,
    int StockMinimo,
    Guid? CategoriaId,
    bool Activo,
    DateTime FechaCreacion,
    DateTime? FechaActualizacion)
{
    public static ProductoResponse Desde(Producto producto) => new(
        producto.Id,
        producto.Nombre,
        producto.Sku,
        producto.Descripcion,
        producto.PrecioCompra,
        producto.PrecioVenta,
        producto.StockMinimo,
        producto.CategoriaId,
        producto.Activo,
        producto.FechaCreacion,
        producto.FechaActualizacion);
}
