using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Bookstores;

namespace MyDigitalLibrary.Api.Endpoints;

public static class FollowedBookSourceEndpoints
{
    public static void MapFollowedBookSourceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/book-sources").WithTags("FollowedBookSources").RequireAuthorization();

        group.MapGet("/", async (FollowedBookSourceService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.ListAsync(currentUser.UserId, ct)));

        group.MapPost("/", async (CreateFollowedBookSourceRequest request, FollowedBookSourceService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var source = await service.CreateAsync(request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/book-sources/{source.Id}", source);
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPut("/{id:guid}", async (Guid id, UpdateFollowedBookSourceRequest request, FollowedBookSourceService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapDelete("/{id:guid}", async (Guid id, FollowedBookSourceService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
