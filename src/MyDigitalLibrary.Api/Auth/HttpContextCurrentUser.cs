using System.Security.Claims;
using MyDigitalLibrary.Application.Abstractions;

namespace MyDigitalLibrary.Api.Auth;

/// <summary>Plan section 8: Domain/Application never see HTTP — this is the one place that reads it.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("No active HTTP context.");

            var idClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("The current request is not authenticated.");

            return Guid.Parse(idClaim);
        }
    }
}
