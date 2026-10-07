namespace StockSync.Inventory.Application.Common;

public interface ITenantActual
{
    Guid TenantId { get; }
}

// Tenant de los datos anteriores a la multitenencia y de los procesos sin solicitud HTTP
// (migraciones, semilla y pruebas).
public static class TenantPorDefecto
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
