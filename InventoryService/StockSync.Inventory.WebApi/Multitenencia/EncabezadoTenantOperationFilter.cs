using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StockSync.Inventory.WebApi.Multitenencia;

public sealed class EncabezadoTenantOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = ResolucionTenantMiddleware.Encabezado,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Tenant de la solicitud. En Development, si se omite se usa el tenant por defecto.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}
