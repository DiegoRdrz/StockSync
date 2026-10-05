using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Categorias;

public sealed record CategoriaResponse(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool Activo,
    DateTime FechaCreacion,
    DateTime? FechaActualizacion)
{
    public static CategoriaResponse Desde(Categoria categoria) => new(
        categoria.Id,
        categoria.Nombre,
        categoria.Descripcion,
        categoria.Activo,
        categoria.FechaCreacion,
        categoria.FechaActualizacion);
}
