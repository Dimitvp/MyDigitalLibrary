using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Api.Endpoints;

public static class LibraryItemEndpoints
{
    public static void MapLibraryItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/library-items").WithTags("LibraryItems").RequireAuthorization();

        group.MapGet("/", async (
            BookFormat? format, OwnershipStatus? status, Guid? shelfId, string? q, Guid? genreId, string? readingStatus,
            string? sortBy, string? sortDir, int? page, int? pageSize,
            LibraryItemService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.ListAsync(format, status, shelfId, q, genreId, readingStatus, sortBy, sortDir, page, pageSize, currentUser.UserId, ct)));

        group.MapPost("/", async (CreateLibraryItemRequest request, LibraryItemService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var item = await service.CreateAsync(request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/library-items/{item.Id}", item);
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapGet("/{id:guid}", async (Guid id, LibraryItemService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.GetAsync(id, currentUser.UserId, ct)));

        group.MapPut("/{id:guid}", async (Guid id, UpdateLibraryItemRequest request, LibraryItemService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapDelete("/{id:guid}", async (Guid id, LibraryItemService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPatch("/{id:guid}/location", async (Guid id, UpdateLocationRequest request, LibraryItemService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.UpdateLocationAsync(id, request.Location, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPatch("/{id:guid}/status", async (Guid id, UpdateStatusRequest request, LibraryItemService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.UpdateStatusAsync(id, request.Status, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPatch("/{id:guid}/format", async (Guid id, UpdateFormatRequest request, LibraryItemService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.ChangeFormatAsync(id, request.Format, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
