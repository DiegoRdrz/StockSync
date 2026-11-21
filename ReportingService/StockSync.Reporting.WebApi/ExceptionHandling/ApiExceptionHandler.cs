using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StockSync.Reporting.Application.Common.Exceptions;

namespace StockSync.Reporting.WebApi.ExceptionHandling;

public sealed class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        if (exception is not ValidationException validation)
            return false;
        context.Response.StatusCode = 400;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ValidationProblemDetails(validation.Errors) { Status = 400, Title = validation.Message }
        });
    }
}
