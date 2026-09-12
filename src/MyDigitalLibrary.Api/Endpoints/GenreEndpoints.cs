using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;

namespace MyDigitalLibrary.Api.Endpoints;

public sealed record GenreDto(Guid Id, string Name);

public static class GenreEndpoints
{
    public static void MapGenreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/genres", async (IApplicationDbContext db, CancellationToken ct) =>
        {
            var genres = await db.Genres.AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new GenreDto(g.Id, g.Name))
                .ToListAsync(ct);

            return Results.Ok(genres);
        })
        .WithTags("Genres")
        .RequireAuthorization();
    }
}
