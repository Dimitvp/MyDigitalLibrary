using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Duplicates;

namespace MyDigitalLibrary.Api.Endpoints;

public static class DuplicateEndpoints
{
    public static void MapDuplicateEndpoints(this IEndpointRouteBuilder app)
    {
        // Not in plan section 4's endpoint list, but explicit Stage 9 scope ("откриване на дубликати").
        app.MapGet("/api/v1/duplicates", async (DuplicateDetectionService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.FindAsync(currentUser.UserId, ct)))
            .WithTags("Duplicates")
            .RequireAuthorization();
    }
}
