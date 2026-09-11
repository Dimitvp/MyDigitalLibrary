using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Infrastructure.Auth;

namespace MyDigitalLibrary.Api.Endpoints;

public sealed record LoginRequest(string Email, string Password);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        // Anonymous, GET (safe): issues the antiforgery cookie + token pair a
        // client reads and echoes back on every state-changing request.
        group.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        group.MapPost("/login", async (LoginRequest request, SignInManager<ApplicationUser> signInManager) =>
        {
            var result = await signInManager.PasswordSignInAsync(request.Email, request.Password, isPersistent: true, lockoutOnFailure: false);

            return result.Succeeded
                ? Results.Ok()
                : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "auth.invalid_credentials");
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>();

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            email = user.FindFirst(ClaimTypes.Email)?.Value,
        })).RequireAuthorization();
    }
}
