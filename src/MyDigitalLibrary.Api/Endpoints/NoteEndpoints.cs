using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Notes;

namespace MyDigitalLibrary.Api.Endpoints;

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var forItem = app.MapGroup("/api/v1/library-items/{libraryItemId:guid}/notes").WithTags("Notes").RequireAuthorization();

        forItem.MapGet("/", async (Guid libraryItemId, NoteService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.ListForLibraryItemAsync(libraryItemId, currentUser.UserId, ct)));

        forItem.MapPost("/", async (Guid libraryItemId, CreateNoteRequest request, NoteService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var note = await service.CreateAsync(libraryItemId, request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/notes/{note.Id}", note);
        }).AddEndpointFilter<AntiforgeryFilter>();

        var byId = app.MapGroup("/api/v1/notes").WithTags("Notes").RequireAuthorization();

        byId.MapPut("/{id:guid}", async (Guid id, UpdateNoteRequest request, NoteService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        byId.MapDelete("/{id:guid}", async (Guid id, NoteService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
