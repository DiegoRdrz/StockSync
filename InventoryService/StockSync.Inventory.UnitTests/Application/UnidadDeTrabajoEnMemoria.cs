using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

internal sealed class UnidadDeTrabajoEnMemoria : IUnidadDeTrabajo
{
    public int Transacciones { get; private set; }

    public async Task<T> EjecutarEnTransaccionAsync<T>(Func<Task<T>> operacion, CancellationToken cancellationToken)
    {
        Transacciones++;
        return await operacion();
    }

    public Task EjecutarEnTransaccionAsync(Func<Task> operacion, CancellationToken cancellationToken)
    {
        Transacciones++;
        return operacion();
    }
}
