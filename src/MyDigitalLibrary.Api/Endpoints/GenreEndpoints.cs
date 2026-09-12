using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;

namespace MyDigitalLibrary.Api.Endpoints;

public sealed record GenreDto(Guid Id, string Name, string? NameEn);

public sealed record RenameGenreRequest(string Name, string? NameEn);

public static class GenreEndpoints
{
    public static void MapGenreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/genres").WithTags("Genres").RequireAuthorization();

        group.MapGet("/", async (IApplicationDbContext db, CancellationToken ct) =>
        {
            var genres = await db.Genres.AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new GenreDto(g.Id, g.Name, g.NameEn))
                .ToListAsync(ct);

            return Results.Ok(genres);
        });

        group.MapPut("/{id:guid}", async (Guid id, RenameGenreRequest request, IApplicationDbContext db, CancellationToken ct) =>
        {
            var genre = await db.Genres.FirstOrDefaultAsync(g => g.Id == id, ct)
                ?? throw new NotFoundException("genre.not_found", $"Genre '{id}' was not found.");

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new AppValidationException("genre.name_required", "Name is required.");

            genre.Rename(request.Name.Trim(), request.NameEn);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        // Removing a genre detaches it from every work that currently
        // references it (Work.GenreIds is a plain uuid[] column, not a FK —
        // nothing else enforces this) before the row itself goes away.
        group.MapDelete("/{id:guid}", async (Guid id, IApplicationDbContext db, CancellationToken ct) =>
        {
            var genre = await db.Genres.FirstOrDefaultAsync(g => g.Id == id, ct)
                ?? throw new NotFoundException("genre.not_found", $"Genre '{id}' was not found.");

            var affectedWorks = await db.Works.Where(w => w.GenreIds.Contains(id)).ToListAsync(ct);
            foreach (var work in affectedWorks)
                work.RemoveGenre(id);

            db.Genres.Remove(genre);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
