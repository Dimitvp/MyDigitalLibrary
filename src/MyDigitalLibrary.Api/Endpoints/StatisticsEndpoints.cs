using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Statistics;

namespace MyDigitalLibrary.Api.Endpoints;

public static class StatisticsEndpoints
{
    public static void MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/statistics", async (int? year, StatisticsService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.GetAsync(year ?? DateTime.UtcNow.Year, currentUser.UserId, ct)))
            .WithTags("Statistics")
            .RequireAuthorization();
    }
}
