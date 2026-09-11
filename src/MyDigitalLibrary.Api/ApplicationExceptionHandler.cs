using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Api;

/// <summary>
/// Maps Application/Domain exceptions to RFC 9457 ProblemDetails with a
/// stable errorCode extension (plan section 4). DomainException defaults to
/// 409 Conflict — it represents an operation that conflicts with the
/// entity's current state, which is what most domain invariants guard
/// against. Anything unrecognized falls through to the default handler.
/// </summary>
public sealed class ApplicationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int statusCode;
        string errorCode;
        IReadOnlyDictionary<string, object?>? extensions = null;

        switch (exception)
        {
            case NotFoundException e:
                statusCode = StatusCodes.Status404NotFound;
                errorCode = e.ErrorCode;
                break;
            case ConflictException e:
                statusCode = StatusCodes.Status409Conflict;
                errorCode = e.ErrorCode;
                extensions = e.Extensions;
                break;
            case AppValidationException e:
                statusCode = StatusCodes.Status400BadRequest;
                errorCode = e.ErrorCode;
                break;
            case DomainException e:
                statusCode = StatusCodes.Status409Conflict;
                errorCode = e.ErrorCode;
                break;
            case ArgumentException:
                statusCode = StatusCodes.Status400BadRequest;
                errorCode = "validation.failed";
                break;
            default:
                return false;
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = errorCode,
            Detail = exception.Message,
        };
        problemDetails.Extensions["errorCode"] = errorCode;

        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
                problemDetails.Extensions[key] = value;
        }

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });

        return true;
    }
}
