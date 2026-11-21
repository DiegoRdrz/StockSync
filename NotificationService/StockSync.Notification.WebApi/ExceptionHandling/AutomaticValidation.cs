using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace StockSync.Notification.WebApi.ExceptionHandling;

// Normalize ASP.NET validation responses for malformed JSON, incorrect types and missing fields
// without exposing internal type names.
public static class AutomaticValidation
{
    private const string GenericMessage = "The submitted value is invalid.";
    private const string RequiredBodyMessage = "The request body is required.";
    private const string BodyField = "Body";

    public static void Configure(MvcOptions options)
    {
        var messages = options.ModelBindingMessageProvider;
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"Value '{value}' is invalid for {field}.");
        messages.SetMissingBindRequiredValueAccessor(field => $"A value for {field} is missing.");
        messages.SetMissingKeyOrValueAccessor(() => "A value is required.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => RequiredBodyMessage);
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Value '{value}' is invalid.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => GenericMessage);
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "The value must be a number.");
        messages.SetUnknownValueIsInvalidAccessor(field => $"The specified value for {field} is invalid.");
        messages.SetValueIsInvalidAccessor(value => $"Value '{value}' is invalid.");
        messages.SetValueMustBeANumberAccessor(field => $"Field {field} must be a number.");
        messages.SetValueMustNotBeNullAccessor(value => $"Value '{value}' is invalid.");

        options.ModelMetadataDetailsProviders.Add(new RequiredMessageProvider());
    }

    public static IActionResult CreateResponse(ActionContext context)
    {
        var bodyParameter = context.ActionDescriptor.Parameters
            .FirstOrDefault(p => p.BindingInfo?.BindingSource == BindingSource.Body)?.Name;
        var entries = context.ModelState.Where(entry => entry.Value is { Errors.Count: > 0 }).ToList();

        if (entries.Count > 1)
            entries.RemoveAll(entry => entry.Key == bodyParameter);

        var errors = entries
            .GroupBy(entry => entry.Key == bodyParameter ? BodyField : FieldName(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group.Key == BodyField
                    ? [RequiredBodyMessage]
                    : group.SelectMany(entry => entry.Value!.Errors).Select(Message).Distinct().ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "One or more fields are invalid.",
            Status = StatusCodes.Status400BadRequest
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static string FieldName(string key)
    {
        var field = key.TrimStart('$', '.');
        return field.Length == 0 ? BodyField : char.ToUpperInvariant(field[0]) + field[1..];
    }

    private static string Message(ModelError error) =>
        string.IsNullOrEmpty(error.ErrorMessage) ? GenericMessage : error.ErrorMessage;

    private sealed class RequiredMessageProvider : IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var required in context.ValidationMetadata.ValidatorMetadata.OfType<RequiredAttribute>())
            {
                if (string.IsNullOrEmpty(required.ErrorMessage) && required.ErrorMessageResourceType is null)
                    required.ErrorMessage = "The {0} field is required.";
            }
        }
    }
}
