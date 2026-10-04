namespace StockSync.Inventory.Application.Common;

public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Items, int Pagina, int TamanoPagina, int Total)
{
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)TamanoPagina);
}
