using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace StockSync.Inventory.WebApi.ExceptionHandling;

// Las respuestas 400 que genera ASP.NET antes de llegar a los validadores (JSON mal formado, tipos
// incorrectos, campos obligatorios) salían en inglés y exponían nombres de tipos internos.
public static class ValidacionAutomatica
{
    private const string MensajeGenerico = "El valor enviado no es válido.";
    private const string MensajeCuerpoObligatorio = "El cuerpo de la solicitud es obligatorio.";
    private const string CampoCuerpo = "Cuerpo";

    public static void Configurar(MvcOptions options)
    {
        var mensajes = options.ModelBindingMessageProvider;
        mensajes.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"El valor '{valor}' no es válido para {campo}.");
        mensajes.SetMissingBindRequiredValueAccessor(campo => $"Falta un valor para {campo}.");
        mensajes.SetMissingKeyOrValueAccessor(() => "Se requiere un valor.");
        mensajes.SetMissingRequestBodyRequiredValueAccessor(() => MensajeCuerpoObligatorio);
        mensajes.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
        mensajes.SetNonPropertyUnknownValueIsInvalidAccessor(() => MensajeGenerico);
        mensajes.SetNonPropertyValueMustBeANumberAccessor(() => "El valor debe ser un número.");
        mensajes.SetUnknownValueIsInvalidAccessor(campo => $"El valor indicado para {campo} no es válido.");
        mensajes.SetValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
        mensajes.SetValueMustBeANumberAccessor(campo => $"El campo {campo} debe ser un número.");
        mensajes.SetValueMustNotBeNullAccessor(valor => $"El valor '{valor}' no es válido.");

        options.ModelMetadataDetailsProviders.Add(new MensajeObligatorioProvider());
    }

    public static IActionResult CrearRespuesta(ActionContext context)
    {
        var parametroCuerpo = context.ActionDescriptor.Parameters
            .FirstOrDefault(p => p.BindingInfo?.BindingSource == BindingSource.Body)?.Name;
        var entradas = context.ModelState.Where(entrada => entrada.Value is { Errors.Count: > 0 }).ToList();

        // Si el JSON no es válido, ASP.NET también marca como obligatorio el parámetro del cuerpo: solo se
        // informa cuando no hay un error más concreto (por ejemplo, un cuerpo "null").
        if (entradas.Count > 1)
            entradas.RemoveAll(entrada => entrada.Key == parametroCuerpo);

        var errores = entradas
            .GroupBy(entrada => entrada.Key == parametroCuerpo ? CampoCuerpo : NombreCampo(entrada.Key))
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Key == CampoCuerpo
                    ? [MensajeCuerpoObligatorio]
                    : grupo.SelectMany(entrada => entrada.Value!.Errors).Select(Mensaje).Distinct().ToArray());

        var problema = new ValidationProblemDetails(errores)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Uno o más campos no son válidos.",
            Status = StatusCodes.Status400BadRequest
        };
        problema.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problema) { ContentTypes = { "application/problem+json" } };
    }

    // "$.cantidad" (ruta JSON) pasa a "Cantidad", como las claves de los validadores de Application.
    private static string NombreCampo(string clave)
    {
        var campo = clave.TrimStart('$', '.');
        return campo.Length == 0 ? CampoCuerpo : char.ToUpperInvariant(campo[0]) + campo[1..];
    }

    // Con AllowInputFormatterExceptionMessages = false, los errores de JSON llegan sin mensaje.
    private static string Mensaje(ModelError error) =>
        string.IsNullOrEmpty(error.ErrorMessage) ? MensajeGenerico : error.ErrorMessage;

    // ASP.NET marca como obligatorios los tipos de referencia no anulables con un RequiredAttribute
    // sin mensaje propio, que usaría el texto en inglés de DataAnnotations.
    private sealed class MensajeObligatorioProvider : IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var requerido in context.ValidationMetadata.ValidatorMetadata.OfType<RequiredAttribute>())
            {
                if (string.IsNullOrEmpty(requerido.ErrorMessage) && requerido.ErrorMessageResourceType is null)
                    requerido.ErrorMessage = "El campo {0} es obligatorio.";
            }
        }
    }
}
