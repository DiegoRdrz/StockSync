using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class UnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly InventoryDbContext _context;

    public UnidadDeTrabajo(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<T> EjecutarEnTransaccionAsync<T>(Func<Task<T>> operacion, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is not null)
            return await operacion();

        // Si la operación falla, la transacción se descarta al liberarse y PostgreSQL revierte todo.
        await using var transaccion = await _context.Database.BeginTransactionAsync(cancellationToken);
        var resultado = await operacion();
        await transaccion.CommitAsync(cancellationToken);
        return resultado;
    }

    public Task EjecutarEnTransaccionAsync(Func<Task> operacion, CancellationToken cancellationToken) =>
        EjecutarEnTransaccionAsync(async () =>
        {
            await operacion();
            return true;
        }, cancellationToken);
}
