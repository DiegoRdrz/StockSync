using Microsoft.Extensions.Primitives;
using StockSync.Identity.Application.Common;
using StockSync.Identity.Application.Common.Exceptions;

namespace StockSync.Identity.WebApi.MultiTenancy;

public sealed class TenantResolutionMiddleware
{
    public const string HeaderName = "X-Tenant-Id";

    private readonly RequestDelegate _next;
    private readonly bool _headerRequired;

    public TenantResolutionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _headerRequired = configuration.GetValue("MultiTenancy:HeaderRequired", false);
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var headerValue = context.Request.Headers[HeaderName];
            if (!StringValues.IsNullOrEmpty(headerValue))
            {
                if (!Guid.TryParse(headerValue, out var tenantId) || tenantId == Guid.Empty)
                    throw new ValidationException(HeaderName, $"The {HeaderName} header must be a valid GUID.");

                context.Items[CurrentTenantHttp.ItemKey] = tenantId;
            }
            else if (_headerRequired && !context.Request.Path.StartsWithSegments("/api/auth"))
            {
                throw new ValidationException(HeaderName, $"The {HeaderName} header is required.");
            }
        }

        return _next(context);
    }
}

