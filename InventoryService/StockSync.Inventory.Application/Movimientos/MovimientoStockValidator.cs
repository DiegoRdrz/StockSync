namespace StockSync.Inventory.Application.Movimientos;

public static class MovimientoStockValidator
{
    public static Dictionary<string, string[]> Validar(MovimientoStockRequest request)
    {
        var errores = new Dictionary<string, string[]>();
        if (request.Cantidad <= 0)
            errores[nameof(request.Cantidad)] = ["La cantidad debe ser mayor que cero."];
        return errores;
    }

    public static Dictionary<string, string[]> Validar(MovimientoStockFiltro filtro)
    {
        var errores = new Dictionary<string, string[]>();
        if (filtro.Pagina < 1)
            errores[nameof(filtro.Pagina)] = ["La página debe ser mayor que cero."];
        if (filtro.TamanoPagina is < 1 or > 100)
            errores[nameof(filtro.TamanoPagina)] = ["El tamaño de página debe estar entre 1 y 100."];
        if (filtro.Pagina > 0 && filtro.TamanoPagina > 0 &&
            (long)(filtro.Pagina - 1) * filtro.TamanoPagina > int.MaxValue)
            errores[nameof(filtro.Pagina)] = ["La página solicitada excede el límite permitido."];
        return errores;
    }
}
