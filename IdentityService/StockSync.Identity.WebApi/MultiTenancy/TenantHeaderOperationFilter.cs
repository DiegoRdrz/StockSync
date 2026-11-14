using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StockSync.Identity.WebApi.MultiTenancy;

public sealed class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = TenantResolutionMiddleware.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Request tenant identifier. Defaults to the default demo tenant in Development mode.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}

