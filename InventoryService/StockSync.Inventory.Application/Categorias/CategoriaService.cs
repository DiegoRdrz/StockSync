using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Categorias;

public class CategoriaService : ICategoriaService
{
    private readonly ICategoriaRepository _categoriaRepository;

    public CategoriaService(ICategoriaRepository categoriaRepository)
    {
        _categoriaRepository = categoriaRepository;
    }

    public async Task<CategoriaResponse> CrearAsync(CategoriaRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(CategoriaValidator.Validar(request));
        await AsegurarNombreDisponibleAsync(request.Nombre, null, cancellationToken);

        var categoria = Categoria.Crear(request.Nombre, request.Descripcion);

        await _categoriaRepository.AgregarAsync(categoria, cancellationToken);
        await _categoriaRepository.GuardarCambiosAsync(cancellationToken);

        return CategoriaResponse.Desde(categoria);
    }

    public async Task<CategoriaResponse> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var categoria = await _categoriaRepository.ObtenerPorIdAsync(id, cancellationToken)
            ?? throw CategoriaNoEncontrada(id);

        return CategoriaResponse.Desde(categoria);
    }

    public async Task<ResultadoPaginado<CategoriaResponse>> ListarAsync(CategoriaFiltro filtro, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(CategoriaValidator.Validar(filtro));

        var (items, total) = await _categoriaRepository.ListarAsync(
            filtro.Nombre,
            (filtro.Pagina - 1) * filtro.TamanoPagina,
            filtro.TamanoPagina,
            cancellationToken);

        return new ResultadoPaginado<CategoriaResponse>(
            items.Select(CategoriaResponse.Desde).ToList(),
            filtro.Pagina,
            filtro.TamanoPagina,
            total);
    }

    public async Task<CategoriaResponse> ActualizarAsync(Guid id, CategoriaRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(CategoriaValidator.Validar(request));

        var categoria = await _categoriaRepository.ObtenerParaActualizarAsync(id, cancellationToken)
            ?? throw CategoriaNoEncontrada(id);

        await AsegurarNombreDisponibleAsync(request.Nombre, id, cancellationToken);

        categoria.Actualizar(request.Nombre, request.Descripcion);
        await _categoriaRepository.GuardarCambiosAsync(cancellationToken);

        return CategoriaResponse.Desde(categoria);
    }

    public async Task EliminarAsync(Guid id, CancellationToken cancellationToken)
    {
        var categoria = await _categoriaRepository.ObtenerParaActualizarAsync(id, cancellationToken)
            ?? throw CategoriaNoEncontrada(id);

        // Se bloquea en lugar de desvincular para no dejar productos sin clasificar de forma silenciosa.
        if (await _categoriaRepository.TieneProductosActivosAsync(id, cancellationToken))
            throw new ConflictException("No se puede eliminar la categoría porque tiene productos activos asociados.");

        categoria.Desactivar();
        await _categoriaRepository.GuardarCambiosAsync(cancellationToken);
    }

    private async Task AsegurarNombreDisponibleAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var nombreNormalizado = Categoria.NormalizarNombre(nombre);
        if (await _categoriaRepository.ExisteNombreAsync(nombreNormalizado, excluirId, cancellationToken))
            throw new ConflictException($"Ya existe una categoría con el nombre '{nombre.Trim()}'.");
    }

    private static void LanzarSiHayErrores(Dictionary<string, string[]> errores)
    {
        if (errores.Count > 0)
            throw new ValidationException(errores);
    }

    private static NotFoundException CategoriaNoEncontrada(Guid id) =>
        new($"No se encontró la categoría con id '{id}'.");
}
