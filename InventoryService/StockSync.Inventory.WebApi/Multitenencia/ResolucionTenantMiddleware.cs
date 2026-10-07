using Microsoft.Extensions.Primitives;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;

namespace StockSync.Inventory.WebApi.Multitenencia;

// El encabezado debe fijarlo el API Gateway tras autenticar al usuario; no se debe aceptar tal cual
// desde clientes públicos. En Development puede omitirse para trabajar con el tenant por defecto.
public sealed class ResolucionTenantMiddleware
{
    public const string Encabezado = "X-Tenant-Id";

    private readonly RequestDelegate _next;
    private readonly bool _encabezadoObligatorio;

    public ResolucionTenantMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _encabezadoObligatorio = configuration.GetValue("Multitenencia:EncabezadoObligatorio", true);
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Items[TenantActualHttp.ClaveItem] = Resolver(context.Request.Headers[Encabezado]);

        return _next(context);
    }

    private Guid Resolver(StringValues valor)
    {
        if (StringValues.IsNullOrEmpty(valor))
            return _encabezadoObligatorio
                ? throw Error($"El encabezado {Encabezado} es obligatorio.")
                : TenantPorDefecto.Id;

        if (!Guid.TryParse(valor, out var tenantId) || tenantId == Guid.Empty)
            throw Error($"El encabezado {Encabezado} debe ser un GUID válido.");

        return tenantId;
    }

    private static ValidationException Error(string mensaje) =>
        new(new Dictionary<string, string[]> { [Encabezado] = [mensaje] });
}
