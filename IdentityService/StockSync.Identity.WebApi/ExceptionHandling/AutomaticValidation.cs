using Microsoft.AspNetCore.Mvc;

namespace StockSync.Identity.WebApi.ExceptionHandling;

public static class AutomaticValidation
{
    public static IActionResult CreateResponse(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };

        return new BadRequestObjectResult(problemDetails);
    }
}

