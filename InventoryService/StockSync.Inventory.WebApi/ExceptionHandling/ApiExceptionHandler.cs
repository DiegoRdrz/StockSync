using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.WebApi.ExceptionHandling;

public class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problemDetails = exception switch
        {
            ValidationException ex => new ValidationProblemDetails(ex.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = ex.Message
            },
            InsufficientStockException ex => Create(StatusCodes.Status409Conflict, "Insufficient stock.", ex.Message),
            DomainException ex => Create(StatusCodes.Status400BadRequest, "Invalid business rule.", ex.Message),
            NotFoundException ex => Create(StatusCodes.Status404NotFound, "Resource not found.", ex.Message),
            ConflictException ex => Create(StatusCodes.Status409Conflict, "Conflict with the current state.", ex.Message),
            _ => null
        };

        // ExceptionHandlerMiddleware logging is disabled in appsettings because .NET 8
        // also logs business exceptions as errors; only unexpected exceptions are logged here.
        if (problemDetails is null)
        {
            _logger.LogError(exception, "Unhandled exception at {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            return false;
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static ProblemDetails Create(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
