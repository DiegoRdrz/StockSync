namespace StockSync.Inventory.Application.Stocks;

// La asignación (producto + sucursal) no cambia nunca: solo se puede modificar la cantidad.
public sealed record StockCantidadRequest(int NuevaCantidad);
