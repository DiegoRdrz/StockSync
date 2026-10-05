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

    private static void Agregar(Dictionary<string, List<string>> errores, string campo, string mensaje)
    {
        if (!errores.TryGetValue(campo, out var mensajes))
            errores[campo] = mensajes = [];
        mensajes.Add(mensaje);
    }

    private static Dictionary<string, string[]> Convertir(Dictionary<string, List<string>> errores) =>
        errores.ToDictionary(e => e.Key, e => e.Value.ToArray());
}
