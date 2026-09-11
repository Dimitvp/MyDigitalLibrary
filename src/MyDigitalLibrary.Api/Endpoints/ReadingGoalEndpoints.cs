using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.ReadingGoals;

namespace MyDigitalLibrary.Api.Endpoints;

public static class ReadingGoalEndpoints
{
    public static void MapReadingGoalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reading-goals").WithTags("ReadingGoals").RequireAuthorization();

        group.MapGet("/{year:int}", async (int year, ReadingGoalService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.GetAsync(year, currentUser.UserId, ct)));

        group.MapPut("/{year:int}", async (int year, UpsertReadingGoalRequest request, ReadingGoalService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.UpsertAsync(year, request, currentUser.UserId, ct))).AddEndpointFilter<AntiforgeryFilter>();
    }
}
