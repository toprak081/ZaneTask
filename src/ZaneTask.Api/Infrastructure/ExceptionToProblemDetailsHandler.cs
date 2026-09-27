using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZaneTask.Application.Common;
using ZaneTask.Domain.Common;

namespace ZaneTask.Api.Infrastructure;

/// <summary>Turns application and domain exceptions into RFC 7807 problem responses.</summary>
internal sealed class ExceptionToProblemDetailsHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            ValidationFailedException e => new ValidationProblemDetails(e.Errors.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
            },
            DomainException e => Problem(StatusCodes.Status400BadRequest, "Request violates a business rule.", e.Message),
            NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not found.", e.Message),
            ForbiddenException e => Problem(StatusCodes.Status403Forbidden, "Forbidden.", e.Message),
            ConflictException e => Problem(StatusCodes.Status409Conflict, "Conflict.", e.Message),
            AuthenticationFailedException e => Problem(StatusCodes.Status401Unauthorized, "Authentication failed.", e.Message),
            _ => null,
        };

        if (problem is null)
            return false;

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
