using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Wishlist;

namespace MyDigitalLibrary.Api.Endpoints;

public static class WishlistEndpoints
{
    public static void MapWishlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/wishlist").WithTags("Wishlist").RequireAuthorization();

        group.MapGet("/", async (WishlistService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.ListAsync(currentUser.UserId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, WishlistService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.GetAsync(id, currentUser.UserId, ct)));

        group.MapPost("/", async (CreateWishlistEntryRequest request, WishlistService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var entry = await service.CreateAsync(request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/wishlist/{entry.Id}", entry);
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/{id:guid}/fulfill", async (Guid id, FulfillWishlistEntryRequest request, WishlistService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var item = await service.FulfillAsync(id, request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/library-items/{item.Id}", item);
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPut("/{id:guid}", async (Guid id, UpdateWishlistEntryRequest request, WishlistService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.UpdateAsync(id, request, currentUser.UserId, ct))).AddEndpointFilter<AntiforgeryFilter>();

        group.MapDelete("/{id:guid}", async (Guid id, WishlistService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/{id:guid}/find-cover", async (Guid id, WishlistService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.FindCoverAsync(id, currentUser.UserId, ct))).AddEndpointFilter<AntiforgeryFilter>();
    }
}
