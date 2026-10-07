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
            StockInsuficienteException ex => Crear(StatusCodes.Status409Conflict, "Stock insuficiente.", ex.Message),
            DomainException ex => Crear(StatusCodes.Status400BadRequest, "Regla de negocio no válida.", ex.Message),
            NotFoundException ex => Crear(StatusCodes.Status404NotFound, "Recurso no encontrado.", ex.Message),
            ConflictException ex => Crear(StatusCodes.Status409Conflict, "Conflicto con el estado actual.", ex.Message),
            _ => null
        };

        // El log del ExceptionHandlerMiddleware está silenciado en appsettings porque en .NET 8
        // registra como error también las excepciones de negocio; aquí solo se registran las inesperadas.
        if (problemDetails is null)
        {
            _logger.LogError(exception, "Excepción no controlada en {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
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

    private static ProblemDetails Crear(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
