using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Categorias;

public static class CategoriaValidator
{
    public static Dictionary<string, string[]> Validar(CategoriaRequest request)
    {
        var errores = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.Nombre))
            Agregar(errores, nameof(request.Nombre), "El nombre es obligatorio.");
        else if (request.Nombre.Trim().Length > Categoria.NombreMaxLength)
            Agregar(errores, nameof(request.Nombre), $"El nombre no puede superar {Categoria.NombreMaxLength} caracteres.");

        if (request.Descripcion?.Trim().Length > Categoria.DescripcionMaxLength)
            Agregar(errores, nameof(request.Descripcion), $"La descripción no puede superar {Categoria.DescripcionMaxLength} caracteres.");

        return Convertir(errores);
    }

    public static Dictionary<string, string[]> Validar(CategoriaFiltro filtro)
    {
        var errores = new Dictionary<string, List<string>>();

        if (filtro.Pagina < 1)
            Agregar(errores, nameof(filtro.Pagina), "La página debe ser mayor o igual a 1.");

        if (filtro.TamanoPagina < 1 || filtro.TamanoPagina > CategoriaFiltro.TamanoPaginaMaximo)
            Agregar(errores, nameof(filtro.TamanoPagina), $"El tamaño de página debe estar entre 1 y {CategoriaFiltro.TamanoPaginaMaximo}.");

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
