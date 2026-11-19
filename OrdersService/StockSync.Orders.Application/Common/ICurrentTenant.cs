namespace StockSync.Orders.Application.Common;

public interface ICurrentTenant
{
    Guid TenantId { get; }
}

// Tenant for processes without an HTTP request (migrations and tests).
// It matches the Inventory default tenant so both services agree in development.
public static class DefaultTenant
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
