namespace StockSync.Inventory.Application.Categorias;

public sealed class CategoriaFiltro
{
    public const int TamanoPaginaMaximo = 100;

    public string? Nombre { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = 20;
}
