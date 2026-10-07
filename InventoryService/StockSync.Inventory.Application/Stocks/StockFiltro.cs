namespace StockSync.Inventory.Application.Stocks;

public class StockFiltro
{
    public const int TamanoPaginaMaximo = 100;

    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = 20;
}

public sealed class StockBajoMinimoFiltro : StockFiltro
{
    public Guid? SucursalId { get; init; }
}
