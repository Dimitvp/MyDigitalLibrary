using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Reading;

namespace MyDigitalLibrary.Api.Endpoints;

public static class ReadingSessionEndpoints
{
    public static void MapReadingSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reading-sessions").WithTags("Reading").RequireAuthorization();

        group.MapGet("/", async (Guid libraryItemId, ReadingSessionService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.ListAsync(libraryItemId, currentUser.UserId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, ReadingSessionService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.GetAsync(id, currentUser.UserId, ct)));

        group.MapPost("/", async (StartReadingSessionRequest request, ReadingSessionService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var session = await service.StartAsync(request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/reading-sessions/{session.Id}", session);
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/{id:guid}/progress", async (Guid id, RecordProgressRequest request, ReadingSessionService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.RecordProgressAsync(id, request, currentUser.UserId, ct))).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/{id:guid}/finish", async (Guid id, FinishReadingSessionRequest request, ReadingSessionService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.FinishAsync(id, request, currentUser.UserId, ct))).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/{id:guid}/abandon", async (Guid id, AbandonReadingSessionRequest request, ReadingSessionService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.AbandonAsync(id, request, currentUser.UserId, ct))).AddEndpointFilter<AntiforgeryFilter>();
    }
}
