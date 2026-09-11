using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Shelves;

namespace MyDigitalLibrary.Api.Endpoints;

public static class ShelfEndpoints
{
    public static void MapShelfEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shelves").WithTags("Shelves").RequireAuthorization();

        group.MapGet("/", async (ShelfService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.ListAsync(currentUser.UserId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, ShelfService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.GetAsync(id, currentUser.UserId, ct)));

        group.MapPost("/", async (CreateShelfRequest request, ShelfService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var shelf = await service.CreateAsync(request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/shelves/{shelf.Id}", shelf);
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPut("/{id:guid}", async (Guid id, RenameShelfRequest request, ShelfService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.RenameAsync(id, request, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapDelete("/{id:guid}", async (Guid id, ShelfService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/{id:guid}/items", async (Guid id, AddShelfItemRequest request, ShelfService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.AddItemAsync(id, request, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapDelete("/{id:guid}/items/{libraryItemId:guid}", async (Guid id, Guid libraryItemId, ShelfService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.RemoveItemAsync(id, libraryItemId, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPut("/{id:guid}/items/{libraryItemId:guid}/order", async (Guid id, Guid libraryItemId, ReorderShelfItemRequest request, ShelfService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.ReorderItemAsync(id, libraryItemId, request, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
