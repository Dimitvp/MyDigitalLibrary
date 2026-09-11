using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Loans;

namespace MyDigitalLibrary.Api.Endpoints;

public static class LoanEndpoints
{
    public static void MapLoanEndpoints(this IEndpointRouteBuilder app)
    {
        var forItem = app.MapGroup("/api/v1/library-items/{libraryItemId:guid}/loans").WithTags("Loans").RequireAuthorization();

        forItem.MapGet("/", async (Guid libraryItemId, LoanService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.ListForLibraryItemAsync(libraryItemId, currentUser.UserId, ct)));

        forItem.MapPost("/", async (Guid libraryItemId, CreateLoanRequest request, LoanService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var loan = await service.CreateAsync(libraryItemId, request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/loans/{loan.Id}", loan);
        }).AddEndpointFilter<AntiforgeryFilter>();

        var byId = app.MapGroup("/api/v1/loans").WithTags("Loans").RequireAuthorization();

        byId.MapPost("/{id:guid}/return", async (Guid id, ReturnLoanRequest request, LoanService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.ReturnAsync(id, request, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
