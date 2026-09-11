using MyDigitalLibrary.Application.Search;

namespace MyDigitalLibrary.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/search", async (string q, SearchService service, CancellationToken ct)
            => Results.Ok(await service.SearchAsync(q, ct)))
            .WithTags("Search")
            .RequireAuthorization();
    }
}
