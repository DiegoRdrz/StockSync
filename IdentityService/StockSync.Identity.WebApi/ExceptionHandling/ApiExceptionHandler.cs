using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StockSync.Identity.Application.Common.Exceptions;
using StockSync.Identity.Domain.Exceptions;

namespace StockSync.Identity.WebApi.ExceptionHandling;

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
            InvalidCredentialsException ex => Create(StatusCodes.Status401Unauthorized, "Invalid credentials.", ex.Message),
            UnauthorizedException ex => Create(StatusCodes.Status403Forbidden, "Forbidden.", ex.Message),
            DomainException ex => Create(StatusCodes.Status400BadRequest, "Business rule violation.", ex.Message),
            NotFoundException ex => Create(StatusCodes.Status404NotFound, "Resource not found.", ex.Message),
            ConflictException ex => Create(StatusCodes.Status409Conflict, "Conflict with current state.", ex.Message),
            _ => null
        };

        if (problemDetails is null)
        {
            _logger.LogError(exception, "Unhandled exception in {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
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

