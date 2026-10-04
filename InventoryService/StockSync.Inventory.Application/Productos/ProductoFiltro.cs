namespace StockSync.Inventory.Application.Productos;

public sealed class ProductoFiltro
{
    public const int TamanoPaginaMaximo = 100;

    public string? Nombre { get; init; }
    public string? Sku { get; init; }
    public Guid? CategoriaId { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = 20;
}
