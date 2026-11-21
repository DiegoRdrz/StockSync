using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StockSync.Notification.WebApi.Multitenancy;

public sealed class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = TenantResolutionMiddleware.Header,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Request tenant. In Development, the default tenant is used when omitted.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}
