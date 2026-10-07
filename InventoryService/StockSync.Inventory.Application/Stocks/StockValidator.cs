namespace StockSync.Inventory.Application.Stocks;

public static class StockValidator
{
    public static Dictionary<string, string[]> Validar(StockRequest request)
    {
        var errores = new Dictionary<string, List<string>>();

        if (request.ProductoId == Guid.Empty)
            Agregar(errores, nameof(request.ProductoId), "El producto es obligatorio.");

        if (request.SucursalId == Guid.Empty)
            Agregar(errores, nameof(request.SucursalId), "La sucursal es obligatoria.");

        if (request.Cantidad < 0)
            Agregar(errores, nameof(request.Cantidad), "La cantidad no puede ser negativa.");

        return Convertir(errores);
    }

    public static Dictionary<string, string[]> Validar(StockCantidadRequest request)
    {
        var errores = new Dictionary<string, List<string>>();

        if (request.NuevaCantidad < 0)
            Agregar(errores, nameof(request.NuevaCantidad), "La cantidad no puede ser negativa.");

        return Convertir(errores);
    }

    public static Dictionary<string, string[]> Validar(StockFiltro filtro)
    {
        var errores = new Dictionary<string, List<string>>();

        if (filtro.Pagina < 1)
            Agregar(errores, nameof(filtro.Pagina), "La página debe ser mayor o igual a 1.");

        if (filtro.TamanoPagina < 1 || filtro.TamanoPagina > StockFiltro.TamanoPaginaMaximo)
            Agregar(errores, nameof(filtro.TamanoPagina), $"El tamaño de página debe estar entre 1 y {StockFiltro.TamanoPaginaMaximo}.");

        // El desplazamiento (Pagina - 1) * TamanoPagina se calcula en int y no debe desbordarse.
        if (filtro.Pagina > 0 && filtro.TamanoPagina > 0 &&
            (long)(filtro.Pagina - 1) * filtro.TamanoPagina > int.MaxValue)
            Agregar(errores, nameof(filtro.Pagina), "La página solicitada excede el límite permitido.");

        return Convertir(errores);
    }

    private static void Agregar(Dictionary<string, List<string>> errores, string campo, string mensaje)
    {
        if (!errores.TryGetValue(campo, out var mensajes))
            errores[campo] = mensajes = [];
        mensajes.Add(mensaje);
    }

    private static Dictionary<string, string[]> Convertir(Dictionary<string, List<string>> errores) =>
        errores.ToDictionary(e => e.Key, e => e.Value.ToArray());
}
