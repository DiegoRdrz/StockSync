using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class CategoriaRepository : ICategoriaRepository
{
    private readonly InventoryDbContext _context;

    public CategoriaRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<Categoria?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, cancellationToken);

    public Task<Categoria?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Categorias
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, cancellationToken);

    public async Task<(IReadOnlyList<Categoria> Items, int Total)> ListarAsync(
        string? nombre,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.Categorias.AsNoTracking().Where(c => c.Activo);

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var patron = $"%{EscaparLike(nombre.Trim())}%";
            query = query.Where(c => EF.Functions.ILike(c.Nombre, patron, @"\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Nombre)
            .ThenBy(c => c.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) =>
        _context.Categorias.AnyAsync(
            c => c.Activo && c.Nombre.ToUpper() == nombre && (excluirId == null || c.Id != excluirId),
            cancellationToken);

    public Task<bool> ExisteActivaAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Categorias.AnyAsync(c => c.Id == id && c.Activo, cancellationToken);

    public Task<bool> TieneProductosActivosAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Productos.AnyAsync(p => p.CategoriaId == id && p.Activo, cancellationToken);

    public async Task AgregarAsync(Categoria categoria, CancellationToken cancellationToken) =>
        await _context.Categorias.AddAsync(categoria, cancellationToken);

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Ya existe una categoría con el mismo nombre.");
        }
    }

    private static string EscaparLike(string valor) =>
        valor.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
