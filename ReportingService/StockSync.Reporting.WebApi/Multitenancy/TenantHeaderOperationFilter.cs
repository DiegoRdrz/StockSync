using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StockSync.Reporting.WebApi.Multitenancy;

public sealed class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = TenantResolutionMiddleware.Header,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Required tenant identifier.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}
