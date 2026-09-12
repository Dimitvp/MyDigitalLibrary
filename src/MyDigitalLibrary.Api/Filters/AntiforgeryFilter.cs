using Microsoft.AspNetCore.Antiforgery;

namespace MyDigitalLibrary.Api.Filters;

/// <summary>
/// Plan section 8: "Anti-forgery токени за state-changing заявки." Applied
/// explicitly per mutating endpoint (not at the route-group level) because
/// GET requests must stay token-free, and ASP.NET Core's automatic
/// antiforgery validation only covers form-bound endpoints, not our JSON bodies.
/// </summary>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "antiforgery.invalid_token",
                detail: ex.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = "antiforgery.invalid_token" });
        }

        return await next(context);
    }
}
