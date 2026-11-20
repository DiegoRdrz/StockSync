using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Application.Common.Exceptions;

namespace StockSync.Reporting.WebApi.Multitenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string Header = "X-Tenant-Id";

    public Task InvokeAsync(HttpContext context, TenantContext tenant)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            if (!Guid.TryParse(context.Request.Headers[Header], out var id) || id == Guid.Empty)
                throw new ValidationException(new Dictionary<string, string[]>() { [Header] = ["A valid tenant GUID is required."] });
            tenant.Set(id);
        }
        return next(context);
    }
}
