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
            var headerValue = context.Request.Headers[Header].ToString();
            if (Guid.TryParse(headerValue, out var id) && id != Guid.Empty)
            {
                tenant.Set(id);
            }
            else
            {
                var claimTenant = context.User.FindFirst("tenant_id")?.Value;
                if (!string.IsNullOrEmpty(claimTenant) && Guid.TryParse(claimTenant, out var claimId) && claimId != Guid.Empty)
                {
                    tenant.Set(claimId);
                }
                else
                {
                    throw new ValidationException(new Dictionary<string, string[]>() { [Header] = ["A valid tenant GUID is required."] });
                }
            }
        }
        return next(context);
    }
}
