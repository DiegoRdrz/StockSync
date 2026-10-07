using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.WebApi.Multitenencia;

public sealed class TenantActualHttp : ITenantActual
{
    public const string ClaveItem = "TenantId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantActualHttp(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Sin solicitud HTTP (migraciones o semilla al arrancar) se usa el tenant por defecto.
    public Guid TenantId => _httpContextAccessor.HttpContext?.Items[ClaveItem] is Guid tenantId
        ? tenantId
        : TenantPorDefecto.Id;
}
